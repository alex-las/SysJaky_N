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

    public IndexModel(ApplicationDbContext context, CartService cartService)
    {
        _context = context;
        _cartService = cartService;
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
        TagOptions = await _context.Tags
            .OrderBy(t => t.Name)
            .Select(t => new SelectListItem
            {
                Text = t.Name,
                Value = t.Id.ToString(),
                Selected = SelectedTagIds.Contains(t.Id)
            })
            .ToListAsync();

        var query = _context.Courses
            .Include(c => c.CourseGroup)
            .Include(c => c.CourseTags)
                .ThenInclude(ct => ct.Tag)
            .AsQueryable();

        if (CourseGroupId.HasValue)
        {
            query = query.Where(c => c.CourseGroupId == CourseGroupId);
        }

        if (!string.IsNullOrWhiteSpace(SearchString))
        {
            var pattern = $"%{SearchString.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.Title, pattern));
        }

        if (Level.HasValue)
        {
            query = query.Where(c => c.Level == Level.Value);
        }

        if (Mode.HasValue)
        {
            query = query.Where(c => c.Mode == Mode.Value);
        }

        var minDuration = MinDuration;
        var maxDuration = MaxDuration;
        if (minDuration.HasValue && maxDuration.HasValue && minDuration > maxDuration)
        {
            (minDuration, maxDuration) = (maxDuration, minDuration);
        }

        if (minDuration.HasValue)
        {
            query = query.Where(c => c.Duration >= minDuration.Value);
        }

        if (maxDuration.HasValue)
        {
            query = query.Where(c => c.Duration <= maxDuration.Value);
        }

        if (SelectedTagIds.Count > 0)
        {
            query = query.Where(c => c.CourseTags.Any(ct => SelectedTagIds.Contains(ct.TagId)));
        }

        query = query.OrderBy(c => c.Date);

        var count = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        Courses = await query.Skip((PageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
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
