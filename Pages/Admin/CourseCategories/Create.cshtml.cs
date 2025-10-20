using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Admin.CourseCategories;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly IStringLocalizer<CreateModel> _localizer;

    public CreateModel(ApplicationDbContext context, ICacheService cacheService, IStringLocalizer<CreateModel> localizer)
    {
        _context = context;
        _cacheService = cacheService;
        _localizer = localizer;
    }

    [BindProperty]
    public CourseCategoryEditorModel Editor { get; set; } = new();

    public IActionResult OnGet()
    {
        ViewData["Title"] = _localizer["Title"];
        Editor.EnsureLocales();

        if (IsAjaxRequest())
        {
            return Partial("_CreateModal", this);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = _localizer["Title"];

        Editor.EnsureLocales();
        NormalizeCategory(Editor.Category);
        ValidateTranslations();

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Partial("_CreateModal", this);
            }

            return Page();
        }

        var category = Editor.Category;

        _context.CourseCategories.Add(category);

        foreach (var translation in Editor.Translations)
        {
            category.Translations.Add(new CourseCategoryTranslation
            {
                Locale = translation.Locale,
                Name = translation.Name,
                Slug = translation.Slug,
                Description = translation.Description
            });
        }

        await _context.SaveChangesAsync();
        _cacheService.InvalidateCourseList();

        if (IsAjaxRequest())
        {
            TempData["StatusMessage"] = $"Kategorie \"{Editor.Category.Name}\" byla vytvořena.";
            return new JsonResult(new { success = true });
        }

        TempData["StatusMessage"] = $"Kategorie \"{Editor.Category.Name}\" byla vytvořena.";
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
