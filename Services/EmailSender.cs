using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using RazorLight;
using SysJaky_N.Data;
using SysJaky_N.EmailTemplates.Models;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class SmtpOptions
{
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; }
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
}

public enum EmailTemplate
{
    Welcome,
    OrderCreated,
    ContactMessageNotification,
    CourseTermCreated,
    WaitlistSeatAvailable,
    CourseReminder,
    CourseReviewRequest,
    NewsletterConfirmation
}

public interface IEmailSender
{
    Task SendEmailAsync<TModel>(string to, EmailTemplate template, TModel model, CancellationToken cancellationToken = default);
}

public class EmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly IRazorLightEngine _razorLightEngine;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EmailSender> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<EmailTemplate, EmailTemplateDescriptor> TemplateMap =
        new Dictionary<EmailTemplate, EmailTemplateDescriptor>
        {
            [EmailTemplate.Welcome] = new(
                "Welcome.cshtml",
                typeof(WelcomeEmailModel),
                _ => "Welcome"),
            [EmailTemplate.OrderCreated] = new(
                "OrderCreated.cshtml",
                typeof(OrderCreatedEmailModel),
                _ => "Order Created"),
            [EmailTemplate.ContactMessageNotification] = new(
                "ContactMessageNotification.cshtml",
                typeof(ContactMessageEmailModel),
                _ => "New contact message"),
            [EmailTemplate.CourseTermCreated] = new(
                "CourseTermCreated.cshtml",
                typeof(CourseTermCreatedEmailModel),
                model =>
                {
                    var data = (CourseTermCreatedEmailModel)model;
                    return $"Nový termín: {data.CourseTitle}";
                }),
            [EmailTemplate.WaitlistSeatAvailable] = new(
                "WaitlistSeatAvailable.cshtml",
                typeof(WaitlistSeatAvailableEmailModel),
                model =>
                {
                    var data = (WaitlistSeatAvailableEmailModel)model;
                    return $"Uvolněné místo: {data.CourseTitle}";
                }),
            [EmailTemplate.CourseReminder] = new(
                "CourseReminder.cshtml",
                typeof(CourseReminderEmailModel),
                model =>
                {
                    var data = (CourseReminderEmailModel)model;
                    return $"Reminder: {data.CourseTitle}";
                }),
            [EmailTemplate.CourseReviewRequest] = new(
                "CourseReviewRequest.cshtml",
                typeof(CourseReviewRequestEmailModel),
                model =>
                {
                    var data = (CourseReviewRequestEmailModel)model;
                    return $"Ohodnoťte kurz {data.CourseTitle}";
                }),
            [EmailTemplate.NewsletterConfirmation] = new(
                "NewsletterConfirmation.cshtml",
                typeof(NewsletterConfirmationEmailModel),
                _ => "Potvrďte odběr newsletteru")
        };

    public EmailSender(
        IOptions<SmtpOptions> options,
        IRazorLightEngine razorLightEngine,
        ApplicationDbContext context,
        ILogger<EmailSender> logger)
    {
        _options = options.Value;
        _razorLightEngine = razorLightEngine;
        _context = context;
        _logger = logger;
    }

    public async Task SendEmailAsync<TModel>(string to, EmailTemplate template, TModel model, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentNullException.ThrowIfNull(model);

        if (!TemplateMap.TryGetValue(template, out var descriptor))
        {
            throw new InvalidOperationException($"Template {template} is not configured.");
        }

        if (!descriptor.ModelType.IsInstanceOfType(model))
        {
            throw new ArgumentException(
                $"Model type {model.GetType().Name} is not valid for template {template}. Expected {descriptor.ModelType.Name}.",
                nameof(model));
        }

        var payloadJson = JsonSerializer.Serialize(model!, descriptor.ModelType, _serializerOptions);
        var log = new EmailLog
        {
            To = to,
            Template = template.ToString(),
            PayloadJson = payloadJson,
            SentUtc = DateTime.UtcNow,
            Status = NormalizeStatus("Pending")
        };

        _context.EmailLogs.Add(log);

        try
        {
            var body = await _razorLightEngine.CompileRenderAsync(descriptor.ViewName, model);
            var subject = descriptor.SubjectFactory(model!);

            await SendMimeMessageAsync(to, subject, body, cancellationToken);

            log.Status = NormalizeStatus("Sent");
        }
        catch (Exception ex)
        {
            log.Status = NormalizeStatus($"Failed: {ex.Message}");
            _logger.LogError(ex, "Failed to send email using template {Template} to {Recipient}.", template, to);
            throw;
        }
        finally
        {
            log.SentUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SendMimeMessageAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Server, _options.Port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_options.User, _options.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private sealed record EmailTemplateDescriptor(string ViewName, Type ModelType, Func<object, string> SubjectFactory);

    private static string NormalizeStatus(string status)
    {
        const int maxLength = 256;
        return status.Length <= maxLength ? status : status[..maxLength];
    }
}
