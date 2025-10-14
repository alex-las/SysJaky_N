using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Extensions;

public static class CourseCardExtensions
{
    private static readonly Regex IsoPattern = new("ISO\\s*(?<code>\\d{4,5})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static CourseCardViewModel ToCourseCardViewModel(
        this Course course,
        CultureInfo culture,
        string detailsUrl,
        string addToCartUrl,
        string wishlistUrl,
        CourseTermSnapshot? termSnapshot)
    {
        int? daysUntilStart = null;
        if (course.Date != default)
        {
            daysUntilStart = (course.Date.Date - DateTime.Today).Days;
        }

        var capacity = termSnapshot?.Capacity ?? 0;
        var seatsTaken = termSnapshot?.SeatsTaken ?? 0;

        return new CourseCardViewModel
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            Level = course.Level.ToString(),
            Mode = course.Mode.ToString(),
            Type = course.Type.ToString(),
            Duration = course.Duration,
            DurationDisplay = CourseCardViewModel.BuildDurationDisplay(course.Duration, culture),
            Date = course.Date,
            DateDisplay = course.Date.ToString("d", culture),
            Price = course.Price,
            PriceDisplay = course.Price.ToString("C", culture),
            CoverImageUrl = course.CoverImageUrl,
            PopoverHtml = course.PopoverHtml,
            DetailsUrl = detailsUrl,
            AddToCartUrl = addToCartUrl,
            WishlistUrl = wishlistUrl,
            IsoStandard = string.IsNullOrWhiteSpace(course.IsoStandard)
                ? null
                : course.IsoStandard.Trim(),
            IsoBadges = ExtractIsoBadges(course),
            Norms = BuildNorms(course),
            Cities = ExtractCities(course),
            DaysUntilStart = daysUntilStart,
            Capacity = capacity,
            SeatsTaken = seatsTaken,
            HasCertificate = HasCertificate(course),
            PreviewContent = BuildPreview(course.Description)
        };
    }

    private static IReadOnlyList<IsoBadgeViewModel> ExtractIsoBadges(Course course)
    {
        if (course.CourseTags == null || course.CourseTags.Count == 0)
        {
            return Array.Empty<IsoBadgeViewModel>();
        }

        var badges = new List<IsoBadgeViewModel>();
        foreach (var tag in course.CourseTags)
        {
            var name = tag.Tag?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var match = IsoPattern.Match(name);
            if (!match.Success)
            {
                continue;
            }

            var code = match.Groups["code"].Value;
            badges.Add(new IsoBadgeViewModel(name.Trim(), code));
        }

        return badges;
    }

    private static IReadOnlyList<string> BuildNorms(Course course)
    {
        var norms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(course.IsoStandard))
        {
            norms.Add(course.IsoStandard.Trim());
        }

        foreach (var badge in ExtractIsoBadges(course))
        {
            if (!string.IsNullOrWhiteSpace(badge.Label))
            {
                norms.Add(badge.Label.Trim());
            }
        }

        if (course.CourseTags != null)
        {
            foreach (var tag in course.CourseTags)
            {
                var name = tag.Tag?.Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (name.Contains("ISO", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("ČSN", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("EN", StringComparison.OrdinalIgnoreCase))
                {
                    norms.Add(name.Trim());
                }
            }
        }

        return norms.Count == 0
            ? Array.Empty<string>()
            : norms.OrderBy(n => n).ToArray();
    }

    private static readonly string[] KnownCityNames =
    {
        "Praha",
        "Brno",
        "Ostrava",
        "Plzeň",
        "Liberec",
        "Olomouc",
        "Hradec Králové",
        "Pardubice",
        "České Budějovice",
        "Zlín"
    };

    private static IReadOnlyList<string> ExtractCities(Course course)
    {
        var cities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (course.CourseTags != null)
        {
            foreach (var tag in course.CourseTags)
            {
                var name = tag.Tag?.Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (KnownCityNames.Any(city => city.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    cities.Add(name.Trim());
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(course.DeliveryForm))
        {
            foreach (var city in KnownCityNames)
            {
                if (course.DeliveryForm.Contains(city, StringComparison.OrdinalIgnoreCase))
                {
                    cities.Add(city);
                }
            }
        }

        return cities.Count == 0
            ? Array.Empty<string>()
            : cities.OrderBy(c => c).ToArray();
    }

    private static bool HasCertificate(Course course)
    {
        if (course.CourseTags == null)
        {
            return false;
        }

        return course.CourseTags.Any(ct =>
            ct.Tag?.Name != null &&
            ct.Tag.Name.Contains("certifik", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildPreview(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        const int MaxLength = 220;
        var trimmed = description.Trim();
        if (trimmed.Length <= MaxLength)
        {
            return trimmed;
        }

        return trimmed[..MaxLength].TrimEnd() + "…";
    }
}

public static class CourseTermSnapshotExtensions
{
    public static async Task<IReadOnlyDictionary<int, CourseTermSnapshot>> LoadTermSnapshotsAsync(
        this ApplicationDbContext context,
        IEnumerable<int> courseIds)
    {
        var idList = courseIds.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<int, CourseTermSnapshot>();
        }

        var nowUtc = DateTime.UtcNow;

        var terms = await context.CourseTerms
            .AsNoTracking()
            .Where(t => idList.Contains(t.CourseId) && t.IsActive)
            .Select(t => new
            {
                t.CourseId,
                t.StartUtc,
                t.Capacity,
                t.SeatsTaken
            })
            .ToListAsync();

        var result = new Dictionary<int, CourseTermSnapshot>();

        foreach (var group in terms.GroupBy(t => t.CourseId))
        {
            var upcoming = group
                .Where(t => t.StartUtc >= nowUtc)
                .OrderBy(t => t.StartUtc)
                .FirstOrDefault();

            var selected = upcoming ?? group.OrderByDescending(t => t.StartUtc).First();
            result[group.Key] = new CourseTermSnapshot(selected.StartUtc, selected.Capacity, selected.SeatsTaken);
        }

        return result;
    }
}

public record CourseTermSnapshot(DateTime StartUtc, int Capacity, int SeatsTaken);
