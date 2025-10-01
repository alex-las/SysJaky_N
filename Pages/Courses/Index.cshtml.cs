using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Services;
using SysJaky_N.Models;
using SysJaky_N.Extensions;
using System.Globalization;

namespace SysJaky_N.Pages.Courses;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly CartService _cartService;
    private readonly ICacheService _cacheService;

    public IndexModel(ApplicationDbContext context, CartService cartService, ICacheService cacheService)
    {
        _context = context;
        _cartService = cartService;
        _cacheService = cacheService;
    }

    public IList<CourseCardViewModel> Courses { get; set; } = new List<CourseCardViewModel>();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? SearchString { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<int> SelectedTagIds { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public List<int> SelectedCityTagIds { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public List<CourseLevel> SelectedLevels { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public List<CourseType> SelectedTypes { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public List<string> SelectedCategoryIds { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public decimal? MinPrice { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? MaxPrice { get; set; }

    public IReadOnlyList<FilterOption> NormOptions { get; private set; } = Array.Empty<FilterOption>();

    public IReadOnlyList<FilterOption> CityOptions { get; private set; } = Array.Empty<FilterOption>();

    public IReadOnlyList<CategoryOption> CategoryOptions { get; private set; } = Array.Empty<CategoryOption>();

    public IReadOnlyList<EnumOption> LevelOptions { get; private set; } = Array.Empty<EnumOption>();

    public IReadOnlyList<EnumOption> TypeOptions { get; private set; } = Array.Empty<EnumOption>();

    public decimal PriceMinimum { get; private set; }

    public decimal PriceMaximum { get; private set; }

    public int TotalPages { get; set; }

    public int TotalCount { get; set; }

    public bool HasActiveFilters { get; private set; }

    private static readonly string[] KnownCityNames = new[]
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

    private const int PageSize = 10;

    private static readonly CourseCategoryDefinition[] CourseCategories =
    {
        new("quality-management", "Systémy managementu kvality (ISO 9001)", new[]
        {
            "Úvod do systému managementu kvality",
            "Manažer kvality (ISO 9001) - certifikační kurz",
            "Interní auditor kvality (ISO 9001)",
            "Procesní řízení v organizaci",
            "Správce dokumentace",
            "Nápravná opatření a 8D reporty"
        }),
        new("environmental-management", "Environmentální management (ISO 14001)", new[]
        {
            "Úvod do EMS",
            "Interní auditor EMS (ISO 14001)",
            "Environmentální aspekty a legislativa"
        }),
        new("integrated-systems", "Integrované systémy", new[]
        {
            "Manažer integrovaného systému (ISO 9001, 14001, 45001)",
            "Auditor integrovaného systému"
        }),
        new("laboratory-accreditation", "Laboratoře - Akreditace (ISO/IEC 17025, ISO 15189)", new[]
        {
            "Manažer kvality laboratoře - základní znalosti",
            "Interní auditor zkušební laboratoře",
            "Metrolog ve zkušební laboratoři",
            "Manažer kvality zdravotnické laboratoře",
            "Validace a verifikace metod"
        }),
        new("occupational-safety", "BOZP (ISO 45001)", new[]
        {
            "Úvod do systému managementu BOZP",
            "Interní auditor BOZP (ISO 45001)",
            "Management rizik BOZP"
        }),
        new("information-security", "Informační bezpečnost (ISO 27001)", new[]
        {
            "Úvod do ISMS",
            "Interní auditor ISMS (ISO 27001)",
            "GDPR a ISO 27001"
        }),
        new("automotive", "Automotive (IATF 16949)", new[]
        {
            "Core Tools (APQP, PPAP, FMEA, MSA, SPC)",
            "Manažer kvality v automotive"
        }),
        new("soft-skills", "Soft skills", new[]
        {
            "Vedení lidí a leadership",
            "Time management",
            "Efektivní komunikace",
            "Prezentační dovednosti"
        })
    };

    private static readonly IReadOnlyDictionary<string, CourseCategoryDefinition> CourseCategoryLookup =
        CourseCategories.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

    public async Task OnGetAsync()
    {
        await InitializeFiltersAsync();

        var filterContext = BuildFilterContext();
        var cacheKey = BuildCourseListCacheKey(filterContext);
        var cacheEntry = await LoadCoursesAsync(filterContext, cacheKey);

        TotalPages = cacheEntry.TotalPages;
        TotalCount = cacheEntry.TotalCount;
        Courses = BuildCourseCards(cacheEntry);
    }

    public async Task<IActionResult> OnGetCoursesAsync()
    {
        await InitializeFiltersAsync();

        var filterContext = BuildFilterContext();
        var cacheKey = BuildCourseListCacheKey(filterContext);
        var cacheEntry = await LoadCoursesAsync(filterContext, cacheKey);

        var courseSummaries = BuildCourseCards(cacheEntry);

        return new JsonResult(new CoursesResponse(
            new PaginationMetadata(filterContext.PageNumber, cacheEntry.TotalPages, cacheEntry.TotalCount),
            courseSummaries,
            new PriceRange(PriceMinimum, PriceMaximum)));
    }

    private async Task InitializeFiltersAsync()
    {
        SelectedTagIds ??= new List<int>();
        SelectedCityTagIds ??= new List<int>();
        SelectedLevels ??= new List<CourseLevel>();
        SelectedTypes ??= new List<CourseType>();
        SelectedCategoryIds ??= new List<string>();

        var allTags = await _context.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new FilterOption(t.Id, t.Name))
            .ToListAsync();

        var normCandidates = allTags
            .Where(t => t.Name.Contains("ISO", StringComparison.OrdinalIgnoreCase)
                || t.Name.Contains("ČSN", StringComparison.OrdinalIgnoreCase)
                || t.Name.Contains("EN", StringComparison.OrdinalIgnoreCase))
            .ToList();
        NormOptions = normCandidates.Count > 0 ? normCandidates : allTags;

        var knownCitySet = new HashSet<string>(KnownCityNames, StringComparer.OrdinalIgnoreCase);
        var cityOptions = allTags
            .Where(t => knownCitySet.Contains(t.Name))
            .ToList();
        CityOptions = cityOptions;

        LevelOptions = Enum.GetValues<CourseLevel>()
            .Select(level => new EnumOption(level.ToString(), level.ToString()))
            .ToList();

        TypeOptions = Enum.GetValues<CourseType>()
            .Select(type => new EnumOption(type.ToString(), type.ToString()))
            .ToList();

        SelectedTagIds = SelectedTagIds
            .Where(id => NormOptions.Any(opt => opt.Id == id))
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        SelectedCityTagIds = SelectedCityTagIds
            .Where(id => CityOptions.Any(opt => opt.Id == id))
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        SelectedLevels = SelectedLevels
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        SelectedTypes = SelectedTypes
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        var activeCourseTitles = await _context.Courses
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => c.Title)
            .ToListAsync();

        var activeTitleSet = new HashSet<string>(activeCourseTitles, StringComparer.OrdinalIgnoreCase);

        CategoryOptions = CourseCategories
            .Select(category =>
            {
                var count = category.TitleSet.Count(title => activeTitleSet.Contains(title));
                return new CategoryOption(category.Id, category.Name, count);
            })
            .ToList();

        var selectedCategorySet = new HashSet<string>(SelectedCategoryIds, StringComparer.OrdinalIgnoreCase);

        SelectedCategoryIds = CourseCategories
            .Where(category => selectedCategorySet.Contains(category.Id))
            .Select(category => category.Id)
            .ToList();

        var priceQuery = _context.Courses
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => c.Price);

        if (await priceQuery.AnyAsync())
        {
            PriceMinimum = await priceQuery.MinAsync();
            PriceMaximum = await priceQuery.MaxAsync();
        }
        else
        {
            PriceMinimum = 0m;
            PriceMaximum = 0m;
        }

        if (!MinPrice.HasValue)
        {
            MinPrice = PriceMinimum;
        }

        if (!MaxPrice.HasValue)
        {
            MaxPrice = PriceMaximum;
        }

        if (MinPrice.HasValue && MaxPrice.HasValue && MinPrice > MaxPrice)
        {
            (MinPrice, MaxPrice) = (MaxPrice, MinPrice);
        }
    }

    private CourseFilterContext BuildFilterContext()
    {
        if (PageNumber < 1)
        {
            PageNumber = 1;
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(SearchString)
            ? null
            : SearchString.Trim();

        var normIds = SelectedTagIds?.Distinct().OrderBy(id => id).ToArray() ?? Array.Empty<int>();
        var cityIds = SelectedCityTagIds?.Distinct().OrderBy(id => id).ToArray() ?? Array.Empty<int>();
        var levelValues = SelectedLevels?.Distinct().OrderBy(l => l).ToArray() ?? Array.Empty<CourseLevel>();
        var typeValues = SelectedTypes?.Distinct().OrderBy(t => t).ToArray() ?? Array.Empty<CourseType>();
        var categoryIds = SelectedCategoryIds?.ToArray() ?? Array.Empty<string>();

        var minPrice = MinPrice;
        var maxPrice = MaxPrice;
        if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice)
        {
            (minPrice, maxPrice) = (maxPrice, minPrice);
            MinPrice = minPrice;
            MaxPrice = maxPrice;
        }

        HasActiveFilters = !string.IsNullOrWhiteSpace(normalizedSearch)
            || normIds.Length > 0
            || cityIds.Length > 0
            || categoryIds.Length > 0
            || levelValues.Length > 0
            || typeValues.Length > 0
            || (minPrice.HasValue && minPrice.Value > PriceMinimum)
            || (maxPrice.HasValue && maxPrice.Value < PriceMaximum);

        var clampedMin = ClampPrice(minPrice, PriceMinimum, PriceMaximum);
        var clampedMax = ClampPrice(maxPrice, PriceMinimum, PriceMaximum);

        if (clampedMin != minPrice)
        {
            MinPrice = clampedMin;
        }

        if (clampedMax != maxPrice)
        {
            MaxPrice = clampedMax;
        }

        return new CourseFilterContext(
            PageNumber,
            normalizedSearch,
            normIds,
            cityIds,
            levelValues,
            typeValues,
            categoryIds,
            clampedMin,
            clampedMax);
    }

    private static decimal? ClampPrice(decimal? value, decimal min, decimal max)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var v = value.Value;
        if (v < min)
        {
            return min;
        }

        if (v > max)
        {
            return max;
        }

        return v;
    }

    private Task<CourseListCacheEntry> LoadCoursesAsync(CourseFilterContext filterContext, string cacheKey)
    {
        return _cacheService.GetCourseListAsync(cacheKey, async () =>
        {
            var query = _context.Courses
                .AsNoTracking()
                .Include(c => c.CourseGroup)
                .Include(c => c.CourseTags)
                    .ThenInclude(ct => ct.Tag)
                .Where(c => c.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filterContext.Search))
            {
                var pattern = $"%{filterContext.Search}%";
                query = query.Where(c => EF.Functions.Like(c.Title, pattern));
            }

            if (filterContext.NormTagIds.Count > 0)
            {
                var tagIds = filterContext.NormTagIds;
                query = query.Where(c => c.CourseTags.Any(ct => tagIds.Contains(ct.TagId)));
            }

            if (filterContext.CityTagIds.Count > 0)
            {
                var cityIds = filterContext.CityTagIds;
                query = query.Where(c => c.CourseTags.Any(ct => cityIds.Contains(ct.TagId)));
            }

            if (filterContext.CategoryIds.Count > 0)
            {
                var selectedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var categoryId in filterContext.CategoryIds)
                {
                    if (CourseCategoryLookup.TryGetValue(categoryId, out var definition))
                    {
                        foreach (var title in definition.TitleSet)
                        {
                            selectedTitles.Add(title);
                        }
                    }
                }

                if (selectedTitles.Count == 0)
                {
                    query = query.Where(static c => false);
                }
                else
                {
                    var titles = selectedTitles.ToArray();
                    query = query.Where(c => titles.Contains(c.Title));
                }
            }

            if (filterContext.Levels.Count > 0)
            {
                var levels = filterContext.Levels;
                query = query.Where(c => levels.Contains(c.Level));
            }

            if (filterContext.Types.Count > 0)
            {
                var types = filterContext.Types;
                query = query.Where(c => types.Contains(c.Type));
            }

            if (filterContext.MinPrice.HasValue)
            {
                var minPrice = filterContext.MinPrice.Value;
                query = query.Where(c => c.Price >= minPrice);
            }

            if (filterContext.MaxPrice.HasValue)
            {
                var maxPrice = filterContext.MaxPrice.Value;
                query = query.Where(c => c.Price <= maxPrice);
            }

            query = query.OrderBy(c => c.Date);

            var count = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(count / (double)PageSize);
            var courses = await query
                .Skip((filterContext.PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var snapshots = await _context.LoadTermSnapshotsAsync(courses.Select(c => c.Id));

            return new CourseListCacheEntry(courses, snapshots, totalPages, count);
        });
    }

    private static string BuildCourseListCacheKey(CourseFilterContext filterContext)
    {
        var searchKey = string.IsNullOrWhiteSpace(filterContext.Search) ? "none" : Uri.EscapeDataString(filterContext.Search);
        var normsKey = filterContext.NormTagIds.Count == 0 ? "none" : string.Join('-', filterContext.NormTagIds);
        var citiesKey = filterContext.CityTagIds.Count == 0 ? "none" : string.Join('-', filterContext.CityTagIds);
        var categoriesKey = filterContext.CategoryIds.Count == 0 ? "none" : string.Join('-', filterContext.CategoryIds);
        var levelsKey = filterContext.Levels.Count == 0 ? "none" : string.Join('-', filterContext.Levels);
        var typesKey = filterContext.Types.Count == 0 ? "none" : string.Join('-', filterContext.Types);
        var minKey = filterContext.MinPrice?.ToString(CultureInfo.InvariantCulture) ?? "null";
        var maxKey = filterContext.MaxPrice?.ToString(CultureInfo.InvariantCulture) ?? "null";

        return $"page={filterContext.PageNumber}|search={searchKey}|norms={normsKey}|cities={citiesKey}|categories={categoriesKey}|levels={levelsKey}|types={typesKey}|minPrice={minKey}|maxPrice={maxKey}";
    }

    private List<CourseCardViewModel> BuildCourseCards(CourseListCacheEntry cacheEntry)
    {
        var culture = CultureInfo.CurrentCulture;
        var addToCartUrl = Url.Page("/Courses/Index", pageHandler: "AddToCart") ?? "/Courses/Index?handler=AddToCart";

        return cacheEntry.Courses
            .Select(course =>
            {
                var detailsUrl = Url.Page("/Courses/Details", new { id = course.Id }) ?? $"/Courses/Details/{course.Id}";
                var wishlistUrl = Url.Page("/Courses/Details", new { id = course.Id, handler = "AddToWishlist" })
                    ?? $"/Courses/Details/{course.Id}?handler=AddToWishlist";

                cacheEntry.TermSnapshots.TryGetValue(course.Id, out var snapshot);

                return course.ToCourseCardViewModel(
                    culture,
                    detailsUrl,
                    addToCartUrl,
                    wishlistUrl,
                    snapshot);
            })
            .ToList();
    }

    private record CourseFilterContext(
        int PageNumber,
        string? Search,
        IReadOnlyList<int> NormTagIds,
        IReadOnlyList<int> CityTagIds,
        IReadOnlyList<CourseLevel> Levels,
        IReadOnlyList<CourseType> Types,
        IReadOnlyList<string> CategoryIds,
        decimal? MinPrice,
        decimal? MaxPrice);

    public record FilterOption(int Id, string Name);

    public record EnumOption(string Value, string Label);

    public record CategoryOption(string Id, string Name, int Count);

    private sealed record CourseCategoryDefinition(string Id, string Name, IReadOnlyCollection<string> Titles)
    {
        public HashSet<string> TitleSet { get; } = Titles
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Select(title => title.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private record CoursesResponse(
        PaginationMetadata Pagination,
        IReadOnlyList<CourseCardViewModel> Courses,
        PriceRange PriceRange);

    private record PaginationMetadata(int PageNumber, int TotalPages, int TotalCount);

    private record PriceRange(decimal Min, decimal Max);

    public async Task<IActionResult> OnPostAddToCartAsync(int courseId)
    {
        var result = await _cartService.AddToCartAsync(HttpContext.Session, courseId);
        if (!result.Success)
        {
            TempData["CartError"] = result.ErrorMessage;
        }
        return RedirectToPage();
    }
}
