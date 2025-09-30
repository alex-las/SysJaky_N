using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using SysJaky_N.EmailTemplates.Models;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class CourseReminderService : ScopedRecurringBackgroundService<CourseReminderService>
{
    public CourseReminderService(IServiceScopeFactory scopeFactory, ILogger<CourseReminderService> logger)
        : base(scopeFactory, logger, RecurringSchedule.FixedDelay(TimeSpan.FromDays(1)))
    {
    }

    protected override string FailureMessage => "Error sending course reminders";

    protected override async Task ExecuteInScopeAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var emailSender = serviceProvider.GetRequiredService<IEmailSender>();
        var certificateService = serviceProvider.GetRequiredService<CertificateService>();

        var today = DateTime.UtcNow.Date;
        var courses = await context.Courses
            .Where(c => c.ReminderDays > 0 && c.Date.Date == today.AddDays(c.ReminderDays))
            .ToListAsync(stoppingToken);

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
