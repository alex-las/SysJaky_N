using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
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
    private readonly INewsletterCategoryProvider _categoryProvider;

    public NewsletterModel(
        ApplicationDbContext context,
        IEmailSender emailSender,
        IStringLocalizer<NewsletterModel> localizer,
        INewsletterCategoryProvider categoryProvider)
    {
        _context = context;
        _emailSender = emailSender;
        _localizer = localizer;
        _categoryProvider = categoryProvider;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool ShowConfirmationResult { get; private set; }

    public bool ConfirmationSucceeded { get; private set; }

    public string ConfirmationTitle { get; private set; } = string.Empty;

    public string ConfirmationMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var categories = await _categoryProvider
            .GetLocalizedCategoriesAsync(CultureInfo.CurrentUICulture, cancellationToken);

        return new JsonResult(new
        {
            categories
        });
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

        var availableCategories = await _categoryProvider
            .GetLocalizedCategoriesAsync(CultureInfo.CurrentUICulture, cancellationToken);
        var selectedCategoryIds = ResolveSelectedCategoryIds(Input.CategoryIds, availableCategories);

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        normalizedEmail = normalizedEmail.ToLowerInvariant();

        var subscriber = await _context.NewsletterSubscribers
            .Include(s => s.PreferredCategories)
            .SingleOrDefaultAsync(s => s.Email == normalizedEmail, cancellationToken);

        if (subscriber is not null && subscriber.ConfirmedAtUtc.HasValue)
        {
            subscriber.ConsentGiven = Input.Consent;
            subscriber.ConsentGivenAtUtc = Input.Consent ? DateTime.UtcNow : subscriber.ConsentGivenAtUtc;
            SynchronizePreferredCategories(subscriber, selectedCategoryIds);
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

        SynchronizePreferredCategories(subscriber, selectedCategoryIds);

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
            .Include(s => s.PreferredCategories)
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

        var availableCategories = await _categoryProvider
            .GetLocalizedCategoriesAsync(CultureInfo.CurrentUICulture, cancellationToken);
        if ((subscriber.PreferredCategories == null || subscriber.PreferredCategories.Count == 0) && availableCategories.Count > 0)
        {
            SynchronizePreferredCategories(subscriber, availableCategories.Select(category => category.Id).ToList());
        }

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

        [Display(Name = "Pages.Api.Newsletter.Input.Categories.DisplayName")]
        public List<int> CategoryIds { get; set; } = new();
    }

    private List<int> ResolveSelectedCategoryIds(
        IReadOnlyCollection<int>? requestedCategoryIds,
        IReadOnlyCollection<NewsletterCategoryOption> availableCategories)
    {
        var availableIds = availableCategories
            .Select(category => category.Id)
            .ToHashSet();

        var selectedIds = new List<int>();

        if (requestedCategoryIds is not null)
        {
            foreach (var categoryId in requestedCategoryIds)
            {
                if (!availableIds.Contains(categoryId))
                {
                    ModelState.AddModelError(nameof(Input.CategoryIds), _localizer["Validation.Categories.Invalid"]);
                    continue;
                }

                if (!selectedIds.Contains(categoryId))
                {
                    selectedIds.Add(categoryId);
                }
            }
        }

        if (selectedIds.Count == 0)
        {
            selectedIds = availableIds.ToList();
        }

        if (selectedIds.Count == 0)
        {
            ModelState.AddModelError(nameof(Input.CategoryIds), _localizer["Validation.Categories.Required"]);
        }

        return selectedIds;
    }

    private void SynchronizePreferredCategories(
        NewsletterSubscriber subscriber,
        IReadOnlyCollection<int> selectedCategoryIds)
    {
        if (subscriber.PreferredCategories == null)
        {
            subscriber.PreferredCategories = new List<NewsletterSubscriberCategory>();
        }

        var preferredCategories = subscriber.PreferredCategories;
        var selectedSet = new HashSet<int>(selectedCategoryIds);

        var toRemove = preferredCategories
            .Where(link => !selectedSet.Contains(link.CourseCategoryId))
            .ToList();

        if (toRemove.Count > 0)
        {
            _context.NewsletterSubscriberCategories.RemoveRange(toRemove);

            foreach (var item in toRemove)
            {
                preferredCategories.Remove(item);
            }
        }

        var existingIds = preferredCategories
            .Select(link => link.CourseCategoryId)
            .ToHashSet();

        foreach (var categoryId in selectedSet)
        {
            if (existingIds.Contains(categoryId))
            {
                continue;
            }

            preferredCategories.Add(new NewsletterSubscriberCategory
            {
                CourseCategoryId = categoryId,
                NewsletterSubscriber = subscriber
            });
        }
    }

}
