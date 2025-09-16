using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class CourseReviewRequestService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CourseReviewRequestService> _logger;
    private readonly Uri _baseUri;
    private readonly string _formPathTemplate;
    private readonly TimeSpan _interval;

    public CourseReviewRequestService(
        IServiceScopeFactory scopeFactory,
        IOptions<CourseReviewRequestOptions> options,
        ILogger<CourseReviewRequestService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var opts = options.Value ?? new CourseReviewRequestOptions();
        if (!Uri.TryCreate(string.IsNullOrWhiteSpace(opts.PublicBaseUrl) ? "https://localhost" : opts.PublicBaseUrl, UriKind.Absolute, out var parsedBase))
        {
            parsedBase = new Uri("https://localhost");
            _logger.LogWarning("Neplatná hodnota CourseReviews:PublicBaseUrl. Používám {BaseUrl} jako výchozí.", parsedBase);
        }

        _baseUri = parsedBase;
        _formPathTemplate = NormalizeTemplate(string.IsNullOrWhiteSpace(opts.FormPathTemplate)
            ? "/Courses/Details/{courseId}"
            : opts.FormPathTemplate);

        var hours = opts.CheckIntervalHours > 0 ? opts.CheckIntervalHours : 24;
        _interval = TimeSpan.FromHours(hours);
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
                _logger.LogError(ex, "Chyba při odesílání žádostí o hodnocení kurzů.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var emailSender = serviceProvider.GetRequiredService<IEmailSender>();

        var now = DateTime.UtcNow;
        var terms = await context.CourseTerms
            .Include(term => term.Course)
            .Where(term => term.EndUtc <= now && term.ReviewRequestSentAtUtc == null)
            .ToListAsync(cancellationToken);

        if (terms.Count == 0)
        {
            return;
        }

        foreach (var term in terms)
        {
            var courseTitle = term.Course?.Title;
            if (string.IsNullOrWhiteSpace(courseTitle))
            {
                courseTitle = $"Termín #{term.Id}";
            }

            var resolvedCourseTitle = courseTitle!;
            var reviewUri = BuildReviewUri(term.CourseId);

            var enrollments = await context.Enrollments
                .Include(enrollment => enrollment.User)
                .Where(enrollment => enrollment.CourseTermId == term.Id && enrollment.Status == EnrollmentStatus.Confirmed)
                .ToListAsync(cancellationToken);

            foreach (var enrollment in enrollments)
            {
                var email = enrollment.User?.Email;
                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogWarning(
                        "Nelze odeslat žádost o hodnocení pro zápis {EnrollmentId}, uživatel {UserId} nemá e-mail.",
                        enrollment.Id,
                        enrollment.UserId);
                    continue;
                }

                var subject = $"Ohodnoťte kurz {resolvedCourseTitle}";
                var bodyBuilder = new StringBuilder();
                bodyBuilder.AppendLine("Dobrý den,");
                bodyBuilder.AppendLine();
                bodyBuilder.AppendLine($"děkujeme za účast na kurzu \"{resolvedCourseTitle}\".");
                bodyBuilder.AppendLine("Budeme rádi, když nám zanecháte hodnocení prostřednictvím formuláře:");
                bodyBuilder.AppendLine(reviewUri.ToString());
                bodyBuilder.AppendLine();
                bodyBuilder.AppendLine("Děkujeme.");

                try
                {
                    await emailSender.SendEmailAsync(email, subject, bodyBuilder.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Odeslání žádosti o hodnocení pro uživatele {Email} selhalo.", email);
                }
            }

            term.ReviewRequestSentAtUtc = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private Uri BuildReviewUri(int courseId)
    {
        var path = _formPathTemplate.Replace("{courseId}", courseId.ToString(CultureInfo.InvariantCulture));
        return new Uri(_baseUri, path);
    }

    private static string NormalizeTemplate(string template)
    {
        var normalized = template.Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized;
    }
}
