using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Admin.CourseCategories;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly IStringLocalizer<EditModel> _localizer;

    public EditModel(ApplicationDbContext context, ICacheService cacheService, IStringLocalizer<EditModel> localizer)
    {
        _context = context;
        _cacheService = cacheService;
        _localizer = localizer;
    }

    [BindProperty]
    public CourseCategoryEditorModel Editor { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        ViewData["Title"] = _localizer["Title"];

        var category = await _context.CourseCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (category == null)
        {
            return NotFound();
        }

        var translations = await _context.coursecategory_translations
            .AsNoTracking()
            .Where(t => t.CategoryId == id)
            .ToListAsync();

        Editor = new CourseCategoryEditorModel
        {
            Category = category,
            Translations = translations.Select(t => new CourseCategoryEditorModel.CourseCategoryTranslationInput
            {
                Locale = t.Locale,
                Name = t.Name,
                Slug = t.Slug,
                Description = t.Description
            }).ToList()
        };

        Editor.EnsureLocales();

        if (IsAjaxRequest())
        {
            return Partial("_EditModal", this);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = _localizer["Title"];

        var categoryToUpdate = await _context.CourseCategories
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Id == Editor.Category.Id);

        if (categoryToUpdate == null)
        {
            return NotFound();
        }

        Editor.EnsureLocales();
        NormalizeCategory(Editor.Category);
        ValidateTranslations();

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Partial("_EditModal", this);
            }

            return Page();
        }

        categoryToUpdate.Name = Editor.Category.Name;
        categoryToUpdate.Slug = Editor.Category.Slug;
        categoryToUpdate.Description = Editor.Category.Description;
        categoryToUpdate.SortOrder = Editor.Category.SortOrder;
        categoryToUpdate.IsActive = Editor.Category.IsActive;

        var existingTranslations = categoryToUpdate.Translations.ToDictionary(t => t.Locale, StringComparer.OrdinalIgnoreCase);

        foreach (var translation in Editor.Translations)
        {
            if (!CourseCategoryEditorModel.SupportedLocales.Contains(translation.Locale))
            {
                continue;
            }

            if (existingTranslations.TryGetValue(translation.Locale, out var existing))
            {
                existing.Name = translation.Name;
                existing.Slug = translation.Slug;
                existing.Description = translation.Description;
                existingTranslations.Remove(translation.Locale);
            }
            else
            {
                categoryToUpdate.Translations.Add(new CourseCategoryTranslation
                {
                    Locale = translation.Locale,
                    Name = translation.Name,
                    Slug = translation.Slug,
                    Description = translation.Description
                });
            }
        }

        foreach (var translation in existingTranslations.Values)
        {
            _context.coursecategory_translations.Remove(translation);
        }

        await _context.SaveChangesAsync();
        _cacheService.InvalidateCourseList();

        if (IsAjaxRequest())
        {
            TempData["StatusMessage"] = $"Kategorie \"{categoryToUpdate.Name}\" byla aktualizována.";
            return new JsonResult(new { success = true });
        }

        TempData["StatusMessage"] = $"Kategorie \"{categoryToUpdate.Name}\" byla aktualizována.";
        return RedirectToPage("Index");
    }

    private static void NormalizeCategory(CourseCategory category)
    {
        category.Name = category.Name?.Trim() ?? string.Empty;
        category.Slug = string.IsNullOrWhiteSpace(category.Slug)
            ? string.Empty
            : category.Slug.Trim().ToLowerInvariant();
        category.Description = string.IsNullOrWhiteSpace(category.Description)
            ? null
            : category.Description.Trim();
        category.SortOrder = category.SortOrder < 0 ? 0 : category.SortOrder;
    }

    private void ValidateTranslations()
    {
        for (int i = 0; i < Editor.Translations.Count; i++)
        {
            var translation = Editor.Translations[i];
            var prefix = $"{nameof(Editor)}.{nameof(Editor.Translations)}[{i}]";

            translation.Locale = translation.Locale?.Trim().ToLowerInvariant() ?? string.Empty;
            translation.Name = translation.Name?.Trim() ?? string.Empty;
            translation.Slug = translation.Slug?.Trim().ToLowerInvariant() ?? string.Empty;
            translation.Description = string.IsNullOrWhiteSpace(translation.Description)
                ? null
                : translation.Description.Trim();

            if (!CourseCategoryEditorModel.SupportedLocales.Contains(translation.Locale))
            {
                if (ModelState.ContainsKey($"{prefix}.{nameof(translation.Locale)}"))
                {
                    ModelState[$"{prefix}.{nameof(translation.Locale)}"].Errors.Clear();
                }

                ModelState.AddModelError($"{prefix}.{nameof(translation.Locale)}", _localizer["TranslationLocaleInvalid"]);
                continue;
            }

            var localeDisplayName = _localizer[$"Locale_{translation.Locale}"].Value ?? translation.Locale;

            if (string.IsNullOrWhiteSpace(translation.Name))
            {
                if (ModelState.ContainsKey($"{prefix}.{nameof(translation.Name)}"))
                {
                    ModelState[$"{prefix}.{nameof(translation.Name)}"].Errors.Clear();
                }

                ModelState.AddModelError($"{prefix}.{nameof(translation.Name)}", _localizer["TranslationNameRequired", localeDisplayName]);
            }

            if (string.IsNullOrWhiteSpace(translation.Slug))
            {
                if (ModelState.ContainsKey($"{prefix}.{nameof(translation.Slug)}"))
                {
                    ModelState[$"{prefix}.{nameof(translation.Slug)}"].Errors.Clear();
                }

                ModelState.AddModelError($"{prefix}.{nameof(translation.Slug)}", _localizer["TranslationSlugRequired", localeDisplayName]);
            }
        }
    }

    private bool IsAjaxRequest() => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
