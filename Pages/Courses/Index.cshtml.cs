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
    public decimal? MinPrice { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? MaxPrice { get; set; }

    public IReadOnlyList<FilterOption> NormOptions { get; private set; } = Array.Empty<FilterOption>();

    public IReadOnlyList<FilterOption> CityOptions { get; private set; } = Array.Empty<FilterOption>();

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
        var levelsKey = filterContext.Levels.Count == 0 ? "none" : string.Join('-', filterContext.Levels);
        var typesKey = filterContext.Types.Count == 0 ? "none" : string.Join('-', filterContext.Types);
        var minKey = filterContext.MinPrice?.ToString(CultureInfo.InvariantCulture) ?? "null";
        var maxKey = filterContext.MaxPrice?.ToString(CultureInfo.InvariantCulture) ?? "null";

        return $"page={filterContext.PageNumber}|search={searchKey}|norms={normsKey}|cities={citiesKey}|levels={levelsKey}|types={typesKey}|minPrice={minKey}|maxPrice={maxKey}";
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
        decimal? MinPrice,
        decimal? MaxPrice);

    public record FilterOption(int Id, string Name);

    public record EnumOption(string Value, string Label);

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
