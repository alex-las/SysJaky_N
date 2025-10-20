using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using System.Security.Claims;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace SysJaky_N.Pages.Courses;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICourseEditor _courseEditor;
    private readonly IStringLocalizer<EditModel> _localizer;

    public IStringLocalizer<EditModel> Localizer => _localizer;

    public EditModel(
        ApplicationDbContext context,
        IAuditService auditService,
        ICourseEditor courseEditor,
        IStringLocalizer<EditModel> localizer)
    {
        _context = context;
        _auditService = auditService;
        _courseEditor = courseEditor;
        _localizer = localizer;
    }

    [BindProperty]
    public Course Course { get; set; } = new();

    [BindProperty]
    public IFormFile? CoverImage { get; set; }

    public SelectList CourseGroups { get; set; } = default!;

    public IEnumerable<SelectListItem> CategoryOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    [BindProperty]
    public List<int> SelectedCategoryIds { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Course? course = await _context.Courses
            .Include(c => c.Categories)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (course == null)
        {
            return NotFound();
        }
        Course = course;
        SelectedCategoryIds = Course.Categories.Select(category => category.Id).ToList();
        await LoadSelectionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        _courseEditor.ValidateCoverImage(CoverImage, ModelState, nameof(CoverImage));

        Course? courseToUpdate = await _context.Courses
            .Include(c => c.Categories)
            .FirstOrDefaultAsync(c => c.Id == Course.Id);
        if (courseToUpdate == null)
        {
            return NotFound();
        }

        await LoadSelectionsAsync();

        if (!ModelState.IsValid)
        {
            Course.CoverImageUrl = courseToUpdate.CoverImageUrl;
            return Page();
        }

        courseToUpdate.Title = Course.Title;
        courseToUpdate.Description = Course.Description;
        courseToUpdate.MetaTitle = Course.MetaTitle;
        courseToUpdate.MetaDescription = Course.MetaDescription;
        courseToUpdate.OpenGraphImage = Course.OpenGraphImage;
        courseToUpdate.CourseGroupId = Course.CourseGroupId;
        courseToUpdate.Price = Course.Price;
        courseToUpdate.Date = Course.Date;
        courseToUpdate.Level = Course.Level;
        courseToUpdate.Mode = Course.Mode;
        courseToUpdate.Duration = Course.Duration;

        courseToUpdate.Categories.Clear();
        if (SelectedCategoryIds.Count > 0)
        {
            var categories = await _context.CourseCategories
                .Where(category => SelectedCategoryIds.Contains(category.Id))
                .ToListAsync();

            foreach (var category in categories)
            {
                courseToUpdate.Categories.Add(category);
            }
        }

        var coverResult = await _courseEditor.SaveCoverImageAsync(
            courseToUpdate.Id,
            CoverImage,
            HttpContext.RequestAborted);

        if (!coverResult.Succeeded)
        {
            ModelState.AddModelError(nameof(CoverImage), coverResult.ErrorMessage ?? string.Empty);
            Course.CoverImageUrl = courseToUpdate.CoverImageUrl;
            return Page();
        }

        if (coverResult.CoverImageUrl is { } coverUrl)
        {
            courseToUpdate.CoverImageUrl = coverUrl;
        }

        try
        {
            await _context.SaveChangesAsync();
            _courseEditor.InvalidateCourseCache(courseToUpdate.Id);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _auditService.LogAsync(userId, "CourseEdited", $"Course {Course.Id} edited");
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.Courses.AnyAsync(e => e.Id == Course.Id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return RedirectToPage("Index");
    }

    private async Task LoadSelectionsAsync()
    {
        var groups = await _context.CourseGroups
            .AsNoTracking()
            .OrderBy(group => group.Name)
            .ToListAsync();

        CourseGroups = new SelectList(groups, "Id", "Name", Course.CourseGroupId);

        SelectedCategoryIds ??= new List<int>();
        var selectedSet = new HashSet<int>(SelectedCategoryIds);

        var localeCandidates = new[]
            {
                CultureInfo.CurrentUICulture.Name,
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            }
            .Where(locale => !string.IsNullOrWhiteSpace(locale))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var categories = await _context.CourseCategories
            .AsNoTracking()
            .Where(category => category.IsActive || selectedSet.Contains(category.Id))
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new { category.Id, category.Name, category.IsActive })
            .ToListAsync();

        var inactiveSuffix = _localizer["InactiveCategorySuffix"].Value ?? " (inactive)";

        var categoryIds = categories.Select(category => category.Id).ToList();

        var translationPriority = localeCandidates
            .Select((locale, index) => new { locale, index })
            .ToDictionary(item => item.locale, item => item.index, StringComparer.OrdinalIgnoreCase);

        Dictionary<int, CourseCategoryTranslation> translationsByCategory = new();

        if (categoryIds.Count > 0 && localeCandidates.Length > 0)
        {
            var translations = await _context.CourseCategoryTranslations
                .AsNoTracking()
                .Where(translation => categoryIds.Contains(translation.CategoryId)
                    && localeCandidates.Contains(translation.Locale))
                .ToListAsync();

            foreach (var grouping in translations.GroupBy(translation => translation.CategoryId))
            {
                var ordered = grouping
                    .OrderBy(translation => translationPriority.TryGetValue(translation.Locale, out var priority)
                        ? priority
                        : int.MaxValue)
                    .FirstOrDefault();

                if (ordered is not null)
                {
                    translationsByCategory[grouping.Key] = ordered;
                }
            }
        }

        CategoryOptions = categories
            .Select(category =>
            {
                var text = category.Name;

                if (translationsByCategory.TryGetValue(category.Id, out var translation)
                    && !string.IsNullOrWhiteSpace(translation.Name))
                {
                    text = translation.Name;
                }

                if (!category.IsActive)
                {
                    text += inactiveSuffix;
                }

                return new SelectListItem(text, category.Id.ToString(), selectedSet.Contains(category.Id));
            })
            .ToList();
    }
}
