using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Services;
using SysJaky_N.Models;

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

    public IList<Course> Courses { get; set; } = new List<Course>();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int? CourseGroupId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchString { get; set; }

    [BindProperty(SupportsGet = true)]
    public CourseLevel? Level { get; set; }

    [BindProperty(SupportsGet = true)]
    public CourseMode? Mode { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? MinDuration { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? MaxDuration { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<int> SelectedTagIds { get; set; } = new();

    public SelectList CourseGroups { get; set; } = default!;

    public IEnumerable<SelectListItem> LevelOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> ModeOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> TagOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    public int TotalPages { get; set; }

    public async Task OnGetAsync()
    {
        const int pageSize = 10;

        if (PageNumber < 1)
        {
            PageNumber = 1;
        }

        CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name");
        LevelOptions = Enum.GetValues<CourseLevel>()
            .Select(level => new SelectListItem
            {
                Text = level.ToString(),
                Value = level.ToString(),
                Selected = Level == level
            })
            .ToList();
        ModeOptions = Enum.GetValues<CourseMode>()
            .Select(mode => new SelectListItem
            {
                Text = mode.ToString(),
                Value = mode.ToString(),
                Selected = Mode == mode
            })
            .ToList();

        SelectedTagIds ??= new List<int>();
        var selectedTagSet = new HashSet<int>(SelectedTagIds);
        TagOptions = await _context.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new SelectListItem
            {
                Text = t.Name,
                Value = t.Id.ToString(),
                Selected = selectedTagSet.Contains(t.Id)
            })
            .ToListAsync();

        var sortedTagIds = selectedTagSet.OrderBy(id => id).ToArray();
        var normalizedSearch = string.IsNullOrWhiteSpace(SearchString) ? null : SearchString.Trim();
        var courseGroupId = CourseGroupId;
        var level = Level;
        var mode = Mode;
        var pageNumber = PageNumber;

        var minDuration = MinDuration;
        var maxDuration = MaxDuration;
        if (minDuration.HasValue && maxDuration.HasValue && minDuration > maxDuration)
        {
            (minDuration, maxDuration) = (maxDuration, minDuration);
        }

        var cacheKey = BuildCourseListCacheKey(
            pageNumber,
            courseGroupId,
            normalizedSearch,
            level,
            mode,
            minDuration,
            maxDuration,
            sortedTagIds);

        var cacheEntry = await _cacheService.GetCourseListAsync(cacheKey, async () =>
        {
            var query = _context.Courses
                .AsNoTracking()
                .Include(c => c.CourseGroup)
                .Include(c => c.CourseTags)
                    .ThenInclude(ct => ct.Tag)
                .AsQueryable();

            if (courseGroupId.HasValue)
            {
                query = query.Where(c => c.CourseGroupId == courseGroupId.Value);
            }

            if (!string.IsNullOrEmpty(normalizedSearch))
            {
                var pattern = $"%{normalizedSearch}%";
                query = query.Where(c => EF.Functions.Like(c.Title, pattern));
            }

            if (level.HasValue)
            {
                query = query.Where(c => c.Level == level.Value);
            }

            if (mode.HasValue)
            {
                query = query.Where(c => c.Mode == mode.Value);
            }

            if (minDuration.HasValue)
            {
                query = query.Where(c => c.Duration >= minDuration.Value);
            }

            if (maxDuration.HasValue)
            {
                query = query.Where(c => c.Duration <= maxDuration.Value);
            }

            if (sortedTagIds.Length > 0)
            {
                var tagIds = sortedTagIds;
                query = query.Where(c => c.CourseTags.Any(ct => tagIds.Contains(ct.TagId)));
            }

            query = query.OrderBy(c => c.Date);

            var count = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);
            var courses = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new CourseListCacheEntry(courses, totalPages);
        });

        TotalPages = cacheEntry.TotalPages;
        Courses = cacheEntry.Courses.ToList();
    }

    private static string BuildCourseListCacheKey(
        int pageNumber,
        int? courseGroupId,
        string? search,
        CourseLevel? level,
        CourseMode? mode,
        int? minDuration,
        int? maxDuration,
        IReadOnlyList<int> tagIds)
    {
        var searchKey = string.IsNullOrWhiteSpace(search) ? "none" : Uri.EscapeDataString(search);
        var tagsKey = tagIds.Count == 0 ? "none" : string.Join('-', tagIds);
        var levelKey = level?.ToString() ?? "null";
        var modeKey = mode?.ToString() ?? "null";
        var groupKey = courseGroupId?.ToString() ?? "null";
        var minKey = minDuration?.ToString() ?? "null";
        var maxKey = maxDuration?.ToString() ?? "null";

        return $"page={pageNumber}|group={groupKey}|search={searchKey}|level={levelKey}|mode={modeKey}|min={minKey}|max={maxKey}|tags={tagsKey}";
    }

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
