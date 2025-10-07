using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using SysJaky_N.EmailTemplates.Models;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class CourseReminderService : ScopedRecurringBackgroundService<CourseReminderService>
{
    private readonly TimeProvider _timeProvider;

    public CourseReminderService(
        IServiceScopeFactory scopeFactory,
        ILogger<CourseReminderService> logger,
        TimeProvider timeProvider)
        : base(scopeFactory, logger, RecurringSchedule.FixedDelay(TimeSpan.FromDays(1)))
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    protected override string FailureMessage => "Error sending course reminders";

    protected override async Task ExecuteInScopeAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var emailSender = serviceProvider.GetRequiredService<IEmailSender>();
        var certificateService = serviceProvider.GetRequiredService<ICertificateService>();

        var todayUtc = _timeProvider.GetUtcNow().UtcDateTime.Date;
        var todayDateTime = DateTime.SpecifyKind(todayUtc, DateTimeKind.Unspecified);

        var eligibleCourses = context.Courses
            .Where(c => c.ReminderDays > 0);

        List<Course> courses;
        if (context.Database.IsRelational())
        {
            courses = await eligibleCourses
                .Where(c => EF.Functions.DateDiffDay(todayDateTime, c.Date) == c.ReminderDays)
                .ToListAsync(stoppingToken);
        }
        else
        {
            var todayDateOnly = DateOnly.FromDateTime(todayDateTime);
            var materializedCourses = await eligibleCourses.ToListAsync(stoppingToken);
            courses = materializedCourses
                .Where(c => DateOnly.FromDateTime(c.Date).DayNumber - todayDateOnly.DayNumber == c.ReminderDays)
                .ToList();
        }

        foreach (var course in courses)
        {
            var orders = await context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                .Where(o => o.Status == OrderStatus.Paid && o.Items.Any(i => i.CourseId == course.Id))
                .ToListAsync(stoppingToken);

            var recipients = orders
                .Select(o => o.User?.Email)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct();

            var model = new CourseReminderEmailModel(course.Title, course.Date, course.Type, course.ReminderMessage);

            foreach (var email in recipients)
            {
                await emailSender.SendEmailAsync(
                    email!,
                    EmailTemplate.CourseReminder,
                    model,
                    stoppingToken);
            }
        }

        await certificateService.IssueCertificatesForCompletedEnrollmentsAsync(stoppingToken);
    }
}
