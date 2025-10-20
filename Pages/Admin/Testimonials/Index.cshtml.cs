using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Authorization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Testimonials;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<IndexModel> _localizer;

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public PublishedFilterState PublishedState { get; set; } = PublishedFilterState.All;

    [BindProperty(SupportsGet = true)]
    public ConsentFilterState ConsentState { get; set; } = ConsentFilterState.All;

    [BindProperty]
    public List<int> SelectedTestimonialIds { get; set; } = new();

    public IList<TestimonialListItem> Testimonials { get; set; } = new List<TestimonialListItem>();

    public TestimonialStatistics Statistics { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostGrantConsentAsync()
    {
        if (SelectedTestimonialIds == null || SelectedTestimonialIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, _localizer["SelectionRequired"]);
            await LoadAsync();
            return Page();
        }

        var testimonials = await _context.Testimonials
            .Where(t => SelectedTestimonialIds.Contains(t.Id))
            .ToListAsync();

        var updated = 0;

        foreach (var testimonial in testimonials)
        {
            if (!testimonial.ConsentGranted)
            {
                testimonial.ConsentGranted = true;
                testimonial.ConsentGrantedAtUtc = DateTime.UtcNow;
                updated++;
            }
            else if (!testimonial.ConsentGrantedAtUtc.HasValue)
            {
                testimonial.ConsentGrantedAtUtc = DateTime.UtcNow;
                updated++;
            }
        }

        if (updated > 0)
        {
            await _context.SaveChangesAsync();
        }

        TempData["StatusMessage"] = _localizer["BulkConsentSuccess", updated];

        return RedirectToPage(new { Search, PublishedState, ConsentState });
    }

    public async Task<IActionResult> OnPostUnpublishAsync()
    {
        if (SelectedTestimonialIds == null || SelectedTestimonialIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, _localizer["SelectionRequired"]);
            await LoadAsync();
            return Page();
        }

        var testimonials = await _context.Testimonials
            .Where(t => SelectedTestimonialIds.Contains(t.Id))
            .ToListAsync();

        var updated = 0;

        foreach (var testimonial in testimonials)
        {
            if (testimonial.IsPublished)
            {
                testimonial.IsPublished = false;
                updated++;
            }
        }

        if (updated > 0)
        {
            await _context.SaveChangesAsync();
        }

        TempData["StatusMessage"] = _localizer["BulkUnpublishSuccess", updated];

        return RedirectToPage(new { Search, PublishedState, ConsentState });
    }

    private async Task LoadAsync()
    {
        ViewData["Title"] = _localizer["Title"];

        var query = _context.Testimonials.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var pattern = $"%{Search.Trim()}%";
            query = query.Where(t =>
                EF.Functions.Like(t.FullName, pattern) ||
                EF.Functions.Like(t.Company, pattern) ||
                EF.Functions.Like(t.Position, pattern) ||
                EF.Functions.Like(t.Quote, pattern));
        }

        query = PublishedState switch
        {
            PublishedFilterState.Published => query.Where(t => t.IsPublished),
            PublishedFilterState.Unpublished => query.Where(t => !t.IsPublished),
            _ => query
        };

        query = ConsentState switch
        {
            ConsentFilterState.Granted => query.Where(t => t.ConsentGranted),
            ConsentFilterState.Missing => query.Where(t => !t.ConsentGranted),
            _ => query
        };

        var orderedQuery = query
            .OrderByDescending(t => t.IsPublished)
            .ThenByDescending(t => t.ConsentGranted)
            .ThenBy(t => t.FullName);

        var total = await query.CountAsync();
        var publishedCount = await query.CountAsync(t => t.IsPublished);
        var consentGrantedCount = await query.CountAsync(t => t.ConsentGranted);
        var awaitingPublication = await query.CountAsync(t => t.ConsentGranted && !t.IsPublished);

        Statistics = new TestimonialStatistics
        {
            Total = total,
            Published = publishedCount,
            AwaitingPublication = awaitingPublication,
            WithConsent = consentGrantedCount,
            WithoutConsent = total - consentGrantedCount
        };

        var now = DateTime.UtcNow;
        Testimonials = (await orderedQuery.ToListAsync())
            .Select(t =>
            {
                DateTime? consentUtc = t.ConsentGrantedAtUtc.HasValue
                    ? DateTime.SpecifyKind(t.ConsentGrantedAtUtc.Value, DateTimeKind.Utc)
                    : null;

                return new TestimonialListItem
                {
                    Id = t.Id,
                    FullName = t.FullName,
                    Position = t.Position,
                    Company = t.Company,
                    Quote = t.Quote,
                    Rating = t.Rating,
                    IsPublished = t.IsPublished,
                    ConsentGranted = t.ConsentGranted,
                    ConsentGrantedAtLocal = consentUtc?.ToLocalTime(),
                    ConsentGrantedAge = consentUtc.HasValue ? now - consentUtc.Value : null
                };
            })
            .ToList();
    }

    public enum PublishedFilterState
    {
        All,
        Published,
        Unpublished
    }

    public enum ConsentFilterState
    {
        All,
        Granted,
        Missing
    }

    public class TestimonialListItem
    {
        public int Id { get; init; }

        public string FullName { get; init; } = string.Empty;

        public string Position { get; init; } = string.Empty;

        public string Company { get; init; } = string.Empty;

        public string Quote { get; init; } = string.Empty;

        public int Rating { get; init; }

        public bool IsPublished { get; init; }

        public bool ConsentGranted { get; init; }

        public DateTime? ConsentGrantedAtLocal { get; init; }

        public TimeSpan? ConsentGrantedAge { get; init; }

        public string PublicationState => IsPublished ? nameof(PublishedFilterState.Published) : nameof(PublishedFilterState.Unpublished);

        public string ConsentState => ConsentGranted ? nameof(ConsentFilterState.Granted) : nameof(ConsentFilterState.Missing);
    }

    public class TestimonialStatistics
    {
        public int Total { get; set; }

        public int Published { get; set; }

        public int AwaitingPublication { get; set; }

        public int WithConsent { get; set; }

        public int WithoutConsent { get; set; }
    }
}
