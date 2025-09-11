using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using SysJaky_N.Models;

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

            var subject = $"Reminder: {course.Title}";
            var message = course.ReminderMessage ?? $"The course {course.Title} is coming on {course.Date:d}.";

            message += course.Type switch
            {
                CourseType.Online => " This course is online.",
                CourseType.InPerson => " This course is in person.",
                _ => " This course can be taken online or in person."
            };

            foreach (var email in recipients)
            {
                await emailSender.SendEmailAsync(email!, subject, message);
            }
        }
    }
}
