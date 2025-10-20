using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Controllers;

[ApiController]
[Route("api/course-calendar")]
public class CourseCalendarController : ControllerBase
{
    private static readonly Regex IsoPattern = new("ISO\\s*(?<code>\\d{4,5})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] KnownCityNames = new[]
    {
        "Praha",
        "Brno",
        "Ostrava",
        "Plzeň",
        "Liberec",
        "Olomouc",
        "České Budějovice",
        "Hradec Králové",
        "Pardubice",
        "Zlín",
        "Jihlava"
    };

    private static readonly Dictionary<string, string> CategoryColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ISO 9001"] = "#1f77b4",
        ["ISO 14001"] = "#2ca02c",
        ["ISO 45001"] = "#ff7f0e",
        ["ISO 27001"] = "#9467bd",
        ["ISO 50001"] = "#17becf",
        ["Online"] = "#0d6efd",
        ["InPerson"] = "#20c997",
        ["Hybrid"] = "#6f42c1"
    };

    private readonly ApplicationDbContext _context;

    public CourseCalendarController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<CourseCalendarResponse>> GetTerms([FromQuery] CourseCalendarQuery query)
    {
        var nowUtc = DateTime.UtcNow.AddMonths(-1);

        var terms = await _context.CourseTerms
            .AsNoTracking()
            .Where(t => t.IsActive && t.EndUtc >= nowUtc)
            .Include(t => t.Course)!
                .ThenInclude(c => c.CourseTags)!
                    .ThenInclude(ct => ct.Tag)
            .Include(t => t.Course)!
                .ThenInclude(c => c.CourseGroup)
            .Include(t => t.Course)!
                .ThenInclude(c => c.Categories)!
                    .ThenInclude(category => category.Translations)
            .ToListAsync();

        var normFilter = BuildStringSet(query.Norms);
        var cityFilter = BuildStringSet(query.Cities);
        var typeFilter = BuildTypeSet(query.Types);
        var onlyAvailable = query.OnlyAvailable ?? false;

        var events = new List<CourseCalendarEventDto>();

        foreach (var term in terms)
        {
            if (term.Course is null)
            {
                continue;
            }

            var tagNames = term.Course.CourseTags
                ?.Select(ct => ct.Tag?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
                .ToList() ?? new List<string>();

            var normTags = tagNames
                .Where(name => IsoPattern.IsMatch(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var cities = tagNames
                .Where(name => KnownCityNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normFilter?.Count > 0 && !normTags.Any(tag => normFilter.Contains(tag)))
            {
                continue;
            }

            if (cityFilter?.Count > 0 && !cities.Any(city => cityFilter.Contains(city)))
            {
                continue;
            }

            if (typeFilter?.Count > 0 && !typeFilter.Contains(term.Course.Type))
            {
                continue;
            }

            var seatsAvailable = Math.Max(0, term.Capacity - term.SeatsTaken);
            if (onlyAvailable && seatsAvailable <= 0)
            {
                continue;
            }

            var category = normTags.FirstOrDefault()
                ?? ResolvePrimaryCategoryName(term.Course)
                ?? term.Course.Type.ToString();

            var color = CategoryColors.TryGetValue(category, out var mapped)
                ? mapped
                : "#0d6efd";

            var primaryCity = cities.FirstOrDefault();

            var eventDto = new CourseCalendarEventDto(
                term.Id,
                term.CourseId,
                term.Course.Title,
                term.StartUtc,
                term.EndUtc,
                category,
                color,
                term.Course.Description,
                term.Course.Type.ToString(),
                term.Course.Mode.ToString(),
                primaryCity,
                normTags,
                cities,
                term.Capacity,
                term.SeatsTaken,
                seatsAvailable,
                Url.Page("/Courses/Details", new { id = term.CourseId }) ?? $"/Courses/Details/{term.CourseId}");

            events.Add(eventDto);
        }

        return Ok(new CourseCalendarResponse(events));
    }

    private static string? ResolvePrimaryCategoryName(Course course)
    {
        if (course.Categories == null || course.Categories.Count == 0)
        {
            return null;
        }

        var culture = CultureInfo.CurrentCulture;
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

        var orderedCategories = course.Categories
            .Where(category => category != null)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (var category in orderedCategories)
        {
            var name = ResolveCategoryName(category, localeCandidates);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return orderedCategories
            .Select(category => category?.Name)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?.Trim();
    }

    private static string? ResolveCategoryName(
        CourseCategory category,
        IReadOnlyList<string> localeCandidates)
    {
        var baseName = category.Name?.Trim();

        if (category.Translations != null && category.Translations.Count > 0)
        {
            foreach (var locale in localeCandidates)
            {
                var translation = category.Translations
                    .FirstOrDefault(t => string.Equals(t.Locale, locale, StringComparison.OrdinalIgnoreCase));

                if (translation != null && !string.IsNullOrWhiteSpace(translation.Name))
                {
                    return translation.Name.Trim();
                }
            }
        }

        return baseName;
    }

    private static HashSet<string>? BuildStringSet(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            set.Add(value.Trim());
        }

        return set.Count == 0 ? null : set;
    }

    private static HashSet<CourseType>? BuildTypeSet(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var set = new HashSet<CourseType>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (Enum.TryParse<CourseType>(value, true, out var parsed))
            {
                set.Add(parsed);
            }
        }

        return set.Count == 0 ? null : set;
    }
}

public record CourseCalendarResponse(IReadOnlyList<CourseCalendarEventDto> Events);

public record CourseCalendarEventDto(
    int TermId,
    int CourseId,
    string Title,
    DateTime StartUtc,
    DateTime EndUtc,
    string Category,
    string Color,
    string? Description,
    string DeliveryType,
    string InstructionMode,
    string? PrimaryCity,
    IReadOnlyList<string> Norms,
    IReadOnlyList<string> Cities,
    int Capacity,
    int SeatsTaken,
    int SeatsAvailable,
    string DetailsUrl);

public class CourseCalendarQuery
{
    [FromQuery(Name = "norms")]
    public List<string>? Norms { get; set; }

    [FromQuery(Name = "cities")]
    public List<string>? Cities { get; set; }

    [FromQuery(Name = "types")]
    public List<string>? Types { get; set; }

    [FromQuery(Name = "onlyAvailable")]
    public bool? OnlyAvailable { get; set; }
}
