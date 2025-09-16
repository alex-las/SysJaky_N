using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class WaitlistNotificationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WaitlistTokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<WaitlistNotificationService> _logger;
    private readonly Uri _baseUri;
    private readonly string _claimPath;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _tokenLifetime = TimeSpan.FromHours(24);
    private readonly Dictionary<int, int> _lastKnownSeats = new();

    public WaitlistNotificationService(
        IServiceScopeFactory scopeFactory,
        WaitlistTokenService tokenService,
        IEmailSender emailSender,
        IOptions<WaitlistOptions> options,
        ILogger<WaitlistNotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _logger = logger;

        var opts = options.Value ?? new WaitlistOptions();
        if (!Uri.TryCreate(string.IsNullOrWhiteSpace(opts.PublicBaseUrl) ? "https://localhost" : opts.PublicBaseUrl, UriKind.Absolute, out var parsedBase))
        {
            parsedBase = new Uri("https://localhost");
            _logger.LogWarning("Neplatná hodnota Waitlist:PublicBaseUrl. Používám {BaseUrl} jako výchozí.", parsedBase);
        }

        _baseUri = parsedBase;
        _claimPath = NormalizePath(opts.ClaimPath);
        var intervalSeconds = opts.PollIntervalSeconds > 0 ? opts.PollIntervalSeconds : 60;
        _pollInterval = TimeSpan.FromSeconds(intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chyba při zpracování pořadníku.");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var terms = await context.CourseTerms
            .AsNoTracking()
            .Where(term => term.IsActive)
            .Select(term => new
            {
                term.Id,
                term.Capacity,
                term.SeatsTaken,
                term.StartUtc,
                CourseTitle = term.Course != null ? term.Course.Title : string.Empty
            })
            .ToListAsync(stoppingToken);

        foreach (var term in terms)
        {
            _lastKnownSeats.TryGetValue(term.Id, out var lastSeats);
            var wasFull = lastSeats >= term.Capacity;
            var seatsDecreased = term.SeatsTaken < lastSeats;
            var seatsAvailable = term.Capacity - term.SeatsTaken;
            var hasAvailability = seatsAvailable > 0;

            _lastKnownSeats[term.Id] = term.SeatsTaken;

            if (!hasAvailability || (!wasFull && !seatsDecreased))
            {
                continue;
            }

            var candidates = await context.WaitlistEntries
                .Include(entry => entry.User)
                .Where(entry => entry.CourseTermId == term.Id
                    && !entry.ReservationConsumed
                    && (!entry.ReservationExpiresAtUtc.HasValue || entry.ReservationExpiresAtUtc <= now))
                .OrderBy(entry => entry.CreatedAtUtc)
                .Take(seatsAvailable)
                .ToListAsync(stoppingToken);

            if (candidates.Count == 0)
            {
                continue;
            }

            var expiration = now.Add(_tokenLifetime);
            var updated = false;
            var courseTitle = string.IsNullOrWhiteSpace(term.CourseTitle) ? $"Termín #{term.Id}" : term.CourseTitle;

            foreach (var entry in candidates)
            {
                if (entry.User?.Email is not { Length: > 0 } email)
                {
                    _logger.LogWarning("Nelze odeslat upozornění pro záznam {EntryId}, uživatel {UserId} nemá e-mail.", entry.Id, entry.UserId);
                    continue;
                }

                var reservationId = Guid.NewGuid();
                var token = _tokenService.CreateToken(entry.Id, reservationId, _tokenLifetime);
                var claimUri = new Uri(_baseUri, $"{_claimPath}?token={WebUtility.UrlEncode(token)}");

                var subject = $"Uvolněné místo: {courseTitle}";
                var bodyBuilder = new StringBuilder();
                bodyBuilder.AppendLine("Dobrý den,");
                bodyBuilder.AppendLine();
                bodyBuilder.AppendLine($"u kurzu \"{courseTitle}\" se uvolnilo místo.");
                bodyBuilder.AppendLine("Rezervujte si jej kliknutím na tento odkaz (platný 24 hodin):");
                bodyBuilder.AppendLine(claimUri.ToString());
                bodyBuilder.AppendLine();
                bodyBuilder.AppendLine("Po uplynutí platnosti odkazu bude místo nabídnuto dalšímu zájemci.");
                bodyBuilder.AppendLine();
                bodyBuilder.AppendLine("Děkujeme.");

                try
                {
                    await _emailSender.SendEmailAsync(email, subject, bodyBuilder.ToString());
                    entry.ReservationToken = reservationId;
                    entry.ReservationExpiresAtUtc = expiration;
                    entry.ReservationConsumed = false;
                    updated = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Odeslání e-mailu pro uživatele {Email} selhalo.", email);
                }
            }

            if (updated)
            {
                await context.SaveChangesAsync(stoppingToken);
            }
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/api/waitlist/claim";
        }

        return path.StartsWith('/') ? path : "/" + path;
    }
}
