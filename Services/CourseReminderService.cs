using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.EmailTemplates.Models;

namespace SysJaky_N.Services;

public class CourseReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CourseReminderService> _logger;

    public CourseReminderService(IServiceScopeFactory scopeFactory, ILogger<CourseReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending course reminders");
            }

            try
            {
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
        }
    }

    private async Task SendRemindersAsync(CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var certificateService = scope.ServiceProvider.GetRequiredService<CertificateService>();

        var today = DateTime.UtcNow.Date;
        var courses = await context.Courses
            .Where(c => c.ReminderDays > 0 && c.Date.Date == today.AddDays(c.ReminderDays))
            .ToListAsync(token);

        foreach (var course in courses)
        {
            var orders = await context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                .Where(o => o.Status == OrderStatus.Paid && o.Items.Any(i => i.CourseId == course.Id))
                .ToListAsync(token);

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
                    token);
            }
        }

        await certificateService.IssueCertificatesForCompletedEnrollmentsAsync(token);
    }
}
