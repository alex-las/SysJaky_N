using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseCategories;

public class CourseCategoryEditorModel
{
    public static readonly string[] SupportedLocales = new[] { "cs", "en" };

    public CourseCategory Category { get; set; } = new();

    public List<CourseCategoryTranslationInput> Translations { get; set; } = new();

    public void EnsureLocales()
    {
        foreach (var locale in SupportedLocales)
        {
            if (!Translations.Any(t => string.Equals(t.Locale, locale, StringComparison.OrdinalIgnoreCase)))
            {
                Translations.Add(new CourseCategoryTranslationInput
                {
                    Locale = locale
                });
            }
        }

        Translations = Translations
            .OrderBy(t => Array.IndexOf(SupportedLocales, t.Locale?.ToLowerInvariant() ?? string.Empty))
            .ToList();
    }

    public class CourseCategoryTranslationInput
    {
        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(10)]
        public string Locale { get; set; } = string.Empty;

        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(100)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }
    }
}
