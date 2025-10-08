using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.EmailTemplates.Models;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Api;

[IgnoreAntiforgeryToken(Order = 1001)]
public class NewsletterModel : PageModel
{
    private const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    private static readonly Regex EmailRegex = new(EmailPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ApplicationDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IStringLocalizer<NewsletterModel> _localizer;

    public NewsletterModel(ApplicationDbContext context, IEmailSender emailSender, IStringLocalizer<NewsletterModel> localizer)
    {
        _context = context;
        _emailSender = emailSender;
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool ShowConfirmationResult { get; private set; }

    public bool ConfirmationSucceeded { get; private set; }

    public string ConfirmationTitle { get; private set; } = string.Empty;

    public string ConfirmationMessage { get; private set; } = string.Empty;

    public IActionResult OnGet()
    {
        return NotFound();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var normalizedEmail = Input.Email?.Trim() ?? string.Empty;

        if (!EmailRegex.IsMatch(normalizedEmail))
        {
            ModelState.AddModelError(nameof(Input.Email), _localizer["Validation.Email.Invalid"]);
        }

        if (!Input.Consent)
        {
            ModelState.AddModelError(nameof(Input.Consent), _localizer["Validation.Consent.Required"]);
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        normalizedEmail = normalizedEmail.ToLowerInvariant();

        var subscriber = await _context.NewsletterSubscribers
            .SingleOrDefaultAsync(s => s.Email == normalizedEmail, cancellationToken);

        if (subscriber is not null && subscriber.ConfirmedAtUtc.HasValue)
        {
            subscriber.ConsentGiven = Input.Consent;
            subscriber.ConsentGivenAtUtc = Input.Consent ? DateTime.UtcNow : subscriber.ConsentGivenAtUtc;
            await _context.SaveChangesAsync(cancellationToken);

            return new JsonResult(new
            {
                success = true,
                message = _localizer["Json.AlreadySubscribed"].Value
            });
        }

        if (subscriber is null)
        {
            subscriber = new NewsletterSubscriber
            {
                Email = normalizedEmail,
                SubscribedAtUtc = DateTime.UtcNow,
                ConsentGiven = Input.Consent,
                ConsentGivenAtUtc = Input.Consent ? DateTime.UtcNow : null,
                ConfirmationToken = Guid.NewGuid().ToString("N")
            };

            _context.NewsletterSubscribers.Add(subscriber);
        }
        else
        {
            subscriber.ConsentGiven = Input.Consent;
            subscriber.ConsentGivenAtUtc = Input.Consent ? DateTime.UtcNow : subscriber.ConsentGivenAtUtc;
            subscriber.SubscribedAtUtc = DateTime.UtcNow;
            subscriber.ConfirmationToken = Guid.NewGuid().ToString("N");
            subscriber.ConfirmedAtUtc = null;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var confirmationUrl = Url.Page(
            "/Api/Newsletter",
            pageHandler: "Confirm",
            values: new { token = subscriber.ConfirmationToken },
            protocol: Request.Scheme);

        await _emailSender.SendEmailAsync(
            normalizedEmail,
            SysJaky_N.Services.EmailTemplate.NewsletterConfirmation,
            new NewsletterConfirmationEmailModel(normalizedEmail, confirmationUrl ?? string.Empty),
            cancellationToken);

        return new JsonResult(new
        {
            success = true,
            message = _localizer["Json.ConfirmationEmailSent"].Value
        });
    }

    public async Task<IActionResult> OnGetConfirmAsync(string? token, CancellationToken cancellationToken)
    {
        ShowConfirmationResult = true;

        if (string.IsNullOrWhiteSpace(token))
        {
            ConfirmationTitle = _localizer["Confirmation.DefaultTitle"];
            ConfirmationMessage = _localizer["Confirmation.InvalidLink"];
            return Page();
        }

        var subscriber = await _context.NewsletterSubscribers
            .SingleOrDefaultAsync(s => s.ConfirmationToken == token, cancellationToken);

        if (subscriber is null)
        {
            ConfirmationTitle = _localizer["Confirmation.DefaultTitle"];
            ConfirmationMessage = _localizer["Confirmation.InvalidOrExpired"];
            return Page();
        }

        if (subscriber.ConfirmedAtUtc.HasValue)
        {
            ConfirmationTitle = _localizer["Confirmation.DefaultTitle"];
            ConfirmationMessage = _localizer["Confirmation.AlreadyConfirmed"];
            ConfirmationSucceeded = true;
            return Page();
        }

        subscriber.ConfirmedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        ConfirmationTitle = _localizer["Confirmation.CompletedTitle"];
        ConfirmationMessage = _localizer["Confirmation.CompletedMessage"];
        ConfirmationSucceeded = true;

        return Page();
    }

    public class InputModel
    {
        [Display(Name = "Pages.Api.Newsletter.Input.Email.DisplayName")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Pages.Api.Newsletter.Input.Consent.DisplayName")]
        public bool Consent { get; set; }
    }
}
