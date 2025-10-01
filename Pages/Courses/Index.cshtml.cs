using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Services;
using SysJaky_N.Models;
using SysJaky_N.Models.ViewModels;
using System.Globalization;
using System.Security.Claims;

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

    public IList<CourseCardViewModel> CourseCards { get; private set; } = new List<CourseCardViewModel>();

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
        CourseCards = (await BuildCourseCardViewModelsAsync(cacheEntry.Courses)).ToList();
    }

    public async Task<IActionResult> OnGetCoursesAsync()
    {
        await InitializeFiltersAsync();

        var filterContext = BuildFilterContext();
        var cacheKey = BuildCourseListCacheKey(filterContext);
        var cacheEntry = await LoadCoursesAsync(filterContext, cacheKey);

        var courseCards = await BuildCourseCardViewModelsAsync(cacheEntry.Courses);
        var courseSummaries = courseCards
            .Select(ToCourseSummary)
            .ToList();

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

            return new CourseListCacheEntry(courses, totalPages, count);
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

    private async Task<IReadOnlyList<CourseCardViewModel>> BuildCourseCardViewModelsAsync(IEnumerable<Course> courses)
    {
        var courseList = courses.ToList();
        var ids = courseList.Select(c => c.Id).ToList();
        var upcomingTerms = await LoadUpcomingTermsAsync(ids);
        var wishlisted = await LoadWishlistedCourseIdsAsync(ids);
        var culture = CultureInfo.CurrentCulture;

        return courseList
            .Select(course =>
            {
                upcomingTerms.TryGetValue(course.Id, out var term);
                return CourseCardViewModelFactory.Create(course, term, wishlisted, Url, culture);
            })
            .ToList();
    }

    private CourseSummary ToCourseSummary(CourseCardViewModel card)
    {
        return new CourseSummary(
            card.Id,
            card.Title,
            card.Description,
            card.Level.ToString(),
            card.Mode.ToString(),
            card.Type.ToString(),
            card.Duration,
            card.DurationDisplay,
            card.DateDisplay,
            card.Price,
            card.PriceDisplay,
            card.CoverImageUrl,
            card.PopoverHtml,
            card.DetailsUrl,
            card.AddToCartUrl,
            card.IsoCertification,
            card.IsoIcon,
            card.OccupancyPercent,
            card.Capacity,
            card.SeatsTaken,
            card.HasCertificate,
            card.CertificateLabel,
            card.PreviewText,
            card.IsWishlisted,
            card.StartDate?.ToString("o"));
    }

    private async Task<Dictionary<int, CourseTerm?>> LoadUpcomingTermsAsync(IEnumerable<int> courseIds)
    {
        var ids = courseIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, CourseTerm?>();
        }

        var nowUtc = DateTime.UtcNow;
        var terms = await _context.CourseTerms
            .AsNoTracking()
            .Where(t => ids.Contains(t.CourseId) && t.IsActive && t.StartUtc >= nowUtc)
            .OrderBy(t => t.StartUtc)
            .ToListAsync();

        var result = new Dictionary<int, CourseTerm?>();
        foreach (var term in terms)
        {
            if (!result.ContainsKey(term.CourseId))
            {
                result[term.CourseId] = term;
            }
        }

        return result;
    }

    private async Task<HashSet<int>> LoadWishlistedCourseIdsAsync(IEnumerable<int> courseIds)
    {
        var ids = courseIds.Distinct().ToList();
        if (ids.Count == 0 || User.Identity?.IsAuthenticated != true)
        {
            return new HashSet<int>();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return new HashSet<int>();
        }

        var wishlisted = await _context.WishlistItems
            .AsNoTracking()
            .Where(w => w.UserId == userId && ids.Contains(w.CourseId))
            .Select(w => w.CourseId)
            .ToListAsync();

        return wishlisted.ToHashSet();
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
        IReadOnlyList<CourseSummary> Courses,
        PriceRange PriceRange);

    private record PaginationMetadata(int PageNumber, int TotalPages, int TotalCount);

    private record PriceRange(decimal Min, decimal Max);

    private record CourseSummary(
        int Id,
        string Title,
        string? Description,
        string Level,
        string Mode,
        string Type,
        int Duration,
        string DurationDisplay,
        string DateDisplay,
        decimal Price,
        string PriceDisplay,
        string? CoverImageUrl,
        string? PopoverHtml,
        string DetailsUrl,
        string AddToCartUrl,
        string? IsoCertification,
        string IsoIcon,
        int OccupancyPercent,
        int? Capacity,
        int? SeatsTaken,
        bool HasCertificate,
        string? CertificateLabel,
        string PreviewText,
        bool IsWishlisted,
        string? StartDateIso);

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
