using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;

namespace SysJaky_N.Services;

public interface INewsletterCategoryProvider
{
    Task<IReadOnlyList<NewsletterCategoryOption>> GetLocalizedCategoriesAsync(
        CultureInfo culture,
        CancellationToken cancellationToken = default);
}

public sealed class NewsletterCategoryProvider : INewsletterCategoryProvider
{
    private readonly ApplicationDbContext _context;

    public NewsletterCategoryProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<NewsletterCategoryOption>> GetLocalizedCategoriesAsync(
        CultureInfo culture,
        CancellationToken cancellationToken = default)
    {
        if (culture is null)
        {
            throw new ArgumentNullException(nameof(culture));
        }

        var localeCandidates = new[]
            {
                culture.Name,
                culture.Parent?.Name,
                culture.TwoLetterISOLanguageName
            }
            .Where(locale => !string.IsNullOrWhiteSpace(locale))
            .Select(locale => locale!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var categories = await _context.CourseCategories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new
            {
                category.Id,
                category.Name,
                category.Slug,
                Translations = category.Translations
                    .Select(translation => new
                    {
                        translation.Locale,
                        translation.Name,
                        translation.Slug
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var options = new List<NewsletterCategoryOption>(categories.Count);

        foreach (var category in categories)
        {
            var displayName = category.Name?.Trim() ?? string.Empty;
            var displaySlug = string.IsNullOrWhiteSpace(category.Slug)
                ? string.Empty
                : category.Slug.Trim();

            foreach (var locale in localeCandidates)
            {
                if (string.IsNullOrWhiteSpace(locale))
                {
                    continue;
                }

                var translation = category.Translations
                    .FirstOrDefault(t => string.Equals(t.Locale, locale, StringComparison.OrdinalIgnoreCase));

                if (translation is null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(translation.Name))
                {
                    displayName = translation.Name.Trim();
                }

                if (!string.IsNullOrWhiteSpace(translation.Slug))
                {
                    displaySlug = translation.Slug.Trim();
                }

                break;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            options.Add(new NewsletterCategoryOption(category.Id, displayName, displaySlug));
        }

        return options;
    }
}

public sealed record NewsletterCategoryOption(int Id, string Name, string Slug);
