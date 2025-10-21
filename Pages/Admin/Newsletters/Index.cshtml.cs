using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Admin.Newsletters;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly INewsletterComposer _newsletterComposer;

    public IndexModel(ApplicationDbContext context, INewsletterComposer newsletterComposer)
    {
        _context = context;
        _newsletterComposer = newsletterComposer;
    }

    public IList<NewsletterSectionCategory> Categories { get; private set; } = new List<NewsletterSectionCategory>();

    public IList<NewsletterSection> Sections { get; private set; } = new List<NewsletterSection>();

    public IList<NewsletterIssue> Issues { get; private set; } = new List<NewsletterIssue>();

    public IList<SelectListItem> CourseCategoryOptions { get; private set; } = new List<SelectListItem>();

    public IList<SelectListItem> SectionCategoryOptions { get; private set; } = new List<SelectListItem>();

    public IList<SelectListItem> SectionOptions { get; private set; } = new List<SelectListItem>();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Newsletter";
        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostSaveCategoryAsync(CategoryInputModel input, CancellationToken cancellationToken)
    {
        input.Name = input.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            ModelState.AddModelError(nameof(CategoryInputModel.Name), "Název je povinný.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        NewsletterSectionCategory entity;
        if (input.Id.HasValue)
        {
            var existing = await _context.NewsletterSectionCategories
                .FirstOrDefaultAsync(category => category.Id == input.Id.Value, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                ModelState.AddModelError(string.Empty, "Kategorie nebyla nalezena.");
                await LoadAsync(cancellationToken).ConfigureAwait(false);
                return Page();
            }

            entity = existing;
        }
        else
        {
            entity = new NewsletterSectionCategory();
            _context.NewsletterSectionCategories.Add(entity);
        }

        entity.Name = input.Name;
        entity.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        entity.CourseCategoryId = input.CourseCategoryId is null or <= 0 ? null : input.CourseCategoryId;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        StatusMessage = input.Id.HasValue ? "Kategorie byla aktualizována." : "Kategorie byla vytvořena.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteCategoryAsync(int id, CancellationToken cancellationToken)
    {
        var category = await _context.NewsletterSectionCategories
            .Include(c => c.Sections)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (category is null)
        {
            StatusMessage = "Kategorie nebyla nalezena.";
            return RedirectToPage();
        }

        if (category.Sections.Any())
        {
            ModelState.AddModelError(string.Empty, "Kategorie obsahuje sekce a nelze ji odstranit.");
            await LoadAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        _context.NewsletterSectionCategories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        StatusMessage = "Kategorie byla odstraněna.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveSectionAsync(SectionInputModel input, CancellationToken cancellationToken)
    {
        input.Title = input.Title?.Trim() ?? string.Empty;
        input.HtmlContent = input.HtmlContent ?? string.Empty;

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            ModelState.AddModelError(nameof(SectionInputModel.Title), "Nadpis je povinný.");
        }

        if (string.IsNullOrWhiteSpace(input.HtmlContent))
        {
            ModelState.AddModelError(nameof(SectionInputModel.HtmlContent), "Obsah je povinný.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        NewsletterSection entity;
        var now = DateTime.UtcNow;

        if (input.Id.HasValue)
        {
            var existing = await _context.NewsletterSections
                .FirstOrDefaultAsync(section => section.Id == input.Id.Value, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                ModelState.AddModelError(string.Empty, "Sekce nebyla nalezena.");
                await LoadAsync(cancellationToken).ConfigureAwait(false);
                return Page();
            }

            entity = existing;
        }
        else
        {
            entity = new NewsletterSection
            {
                CreatedAtUtc = now
            };
            _context.NewsletterSections.Add(entity);
        }

        entity.Title = input.Title;
        entity.HtmlContent = input.HtmlContent;
        entity.IsPublished = input.IsPublished;
        var sortOrder = input.SortOrder;
        if (!input.Id.HasValue && sortOrder == 0)
        {
            var maxSortOrder = await _context.NewsletterSections
                .AsNoTracking()
                .MaxAsync(section => (int?)section.SortOrder, cancellationToken)
                .ConfigureAwait(false) ?? 0;
            sortOrder = maxSortOrder + 1;
        }

        entity.SortOrder = sortOrder;
        entity.NewsletterSectionCategoryId = input.NewsletterSectionCategoryId;
        entity.UpdatedAtUtc = now;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        StatusMessage = input.Id.HasValue ? "Sekce byla aktualizována." : "Sekce byla vytvořena.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteSectionAsync(int id, CancellationToken cancellationToken)
    {
        var section = await _context.NewsletterSections
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (section is null)
        {
            StatusMessage = "Sekce nebyla nalezena.";
            return RedirectToPage();
        }

        _context.NewsletterSections.Remove(section);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        StatusMessage = "Sekce byla odstraněna.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateIssueAsync(IssueInputModel input, CancellationToken cancellationToken)
    {
        input.Subject = input.Subject?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input.Subject))
        {
            ModelState.AddModelError(nameof(IssueInputModel.Subject), "Předmět je povinný.");
        }

        if (input.SelectedCategoryIds is null || input.SelectedCategoryIds.Count == 0)
        {
            ModelState.AddModelError(nameof(IssueInputModel.SelectedCategoryIds), "Vyberte alespoň jednu kategorii.");
        }

        if (input.SelectedSectionIds is null || input.SelectedSectionIds.Count == 0)
        {
            ModelState.AddModelError(nameof(IssueInputModel.SelectedSectionIds), "Vyberte alespoň jednu sekci.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        var distinctCategoryIds = input.SelectedCategoryIds!.Distinct().ToArray();
        var distinctSectionIds = input.SelectedSectionIds!.Distinct().ToArray();

        var selectedSections = await _context.NewsletterSections
            .AsNoTracking()
            .Where(section => distinctSectionIds.Contains(section.Id))
            .Select(section => new
            {
                section.Id,
                section.NewsletterSectionCategoryId,
                section.IsPublished
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (selectedSections.Count != distinctSectionIds.Length)
        {
            ModelState.AddModelError(nameof(IssueInputModel.SelectedSectionIds), "Vybrané sekce nebyly nalezeny.");
        }

        var allowedCategorySet = distinctCategoryIds.ToHashSet();

        foreach (var section in selectedSections)
        {
            if (!allowedCategorySet.Contains(section.NewsletterSectionCategoryId))
            {
                ModelState.AddModelError(nameof(IssueInputModel.SelectedSectionIds), "Sekce musí odpovídat vybraným kategoriím.");
            }

            if (!section.IsPublished)
            {
                ModelState.AddModelError(nameof(IssueInputModel.SelectedSectionIds), "Do newsletteru lze přidat pouze publikované sekce.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        var issue = new NewsletterIssue
        {
            Subject = input.Subject,
            Preheader = string.IsNullOrWhiteSpace(input.Preheader) ? null : input.Preheader.Trim(),
            IntroHtml = string.IsNullOrWhiteSpace(input.IntroHtml) ? null : input.IntroHtml,
            OutroHtml = string.IsNullOrWhiteSpace(input.OutroHtml) ? null : input.OutroHtml,
            CreatedAtUtc = DateTime.UtcNow,
            ScheduledForUtc = input.ScheduledForUtc
        };
        foreach (var categoryId in distinctCategoryIds)
        {
            issue.Categories.Add(new NewsletterIssueCategory
            {
                NewsletterSectionCategoryId = categoryId
            });
        }

        for (var index = 0; index < distinctSectionIds.Length; index++)
        {
            var sectionId = distinctSectionIds[index];
            issue.Sections.Add(new NewsletterIssueSection
            {
                NewsletterSectionId = sectionId,
                SortOrder = index
            });
        }

        _context.NewsletterIssues.Add(issue);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        StatusMessage = "Newsletter byl vytvořen.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSendIssueAsync(int id, CancellationToken cancellationToken)
    {
        var sentCount = await _newsletterComposer.ComposeAndSendIssueAsync(id, cancellationToken).ConfigureAwait(false);
        StatusMessage = sentCount == 0
            ? "Newsletter neměl žádný obsah k odeslání."
            : $"Newsletter byl odeslán {sentCount} odběratelům.";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Newsletter";
        Categories = await _context.NewsletterSectionCategories
            .AsNoTracking()
            .Include(category => category.CourseCategory)
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Sections = await _context.NewsletterSections
            .AsNoTracking()
            .Include(section => section.Category)
            .OrderBy(section => section.SortOrder)
            .ThenBy(section => section.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Issues = await _context.NewsletterIssues
            .AsNoTracking()
            .Include(issue => issue.Categories)
                .ThenInclude(issueCategory => issueCategory.NewsletterSectionCategory)
            .Include(issue => issue.Sections)
                .ThenInclude(issueSection => issueSection.NewsletterSection)
                .ThenInclude(section => section.Category)
            .OrderByDescending(issue => issue.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        CourseCategoryOptions = await _context.CourseCategories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new SelectListItem
            {
                Value = category.Id.ToString(),
                Text = category.Name
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        SectionCategoryOptions = Categories
            .Select(category => new SelectListItem
            {
                Value = category.Id.ToString(),
                Text = category.Name
            })
            .ToList();

        SectionOptions = Sections
            .Where(section => section.IsPublished)
            .Select(section => new SelectListItem
            {
                Value = section.Id.ToString(),
                Text = $"{section.Title} ({section.Category?.Name})"
            })
            .ToList();
    }

    public sealed class CategoryInputModel
    {
        public int? Id { get; set; }

        [Required]
        [MaxLength(128)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? Description { get; set; }

        public int? CourseCategoryId { get; set; }
    }

    public sealed class SectionInputModel
    {
        public int? Id { get; set; }

        [Required]
        [MaxLength(180)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string HtmlContent { get; set; } = string.Empty;

        public bool IsPublished { get; set; }

        [Range(0, int.MaxValue)]
        public int SortOrder { get; set; }

        [Required]
        public int NewsletterSectionCategoryId { get; set; }
    }

    public sealed class IssueInputModel
    {
        [Required]
        [MaxLength(180)]
        public string Subject { get; set; } = string.Empty;

        [MaxLength(180)]
        public string? Preheader { get; set; }

        public string? IntroHtml { get; set; }

        public string? OutroHtml { get; set; }

        public DateTime? ScheduledForUtc { get; set; }

        public List<int> SelectedCategoryIds { get; set; } = new();

        public List<int> SelectedSectionIds { get; set; } = new();
    }
}
