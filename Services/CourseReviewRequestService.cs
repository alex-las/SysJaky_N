using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SysJaky_N.Data;
using SysJaky_N.EmailTemplates.Models;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class CourseReviewRequestService : ScopedRecurringBackgroundService<CourseReviewRequestService>
{
    private readonly ILogger<CourseReviewRequestService> _logger;
    private readonly IStringLocalizer<CourseReviewRequestService> _localizer;
    private readonly Uri _baseUri;
    private readonly string _formPathTemplate;

    public CourseReviewRequestService(
        IServiceScopeFactory scopeFactory,
        IOptions<CourseReviewRequestOptions> options,
        ILogger<CourseReviewRequestService> logger,
        IStringLocalizer<CourseReviewRequestService> localizer)
        : base(scopeFactory, logger, RecurringSchedule.FixedDelay(GetInterval(options)))
    {
        _logger = logger;
        _localizer = localizer;

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
    }

    protected override string FailureMessage => "Chyba při odesílání žádostí o hodnocení kurzů.";

    protected override async Task ExecuteInScopeAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        await ProcessAsync(serviceProvider, stoppingToken);
    }

    private static TimeSpan GetInterval(IOptions<CourseReviewRequestOptions> optionsAccessor)
    {
        var opts = optionsAccessor.Value ?? new CourseReviewRequestOptions();
        var hours = opts.CheckIntervalHours > 0 ? opts.CheckIntervalHours : 24;
        return TimeSpan.FromHours(hours);
    }

    private async Task ProcessAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
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
            var title = term.Course?.Title;
            var resolvedCourseTitle = string.IsNullOrWhiteSpace(title)
                ? _localizer["FallbackTerm", term.Id].Value
                : title;
            var reviewUri = BuildReviewUri(term.CourseId);
            var emailModel = new CourseReviewRequestEmailModel(resolvedCourseTitle, reviewUri.ToString());

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

                try
                {
                    await emailSender.SendEmailAsync(
                        email,
                        EmailTemplate.CourseReviewRequest,
                        emailModel,
                        cancellationToken);
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
