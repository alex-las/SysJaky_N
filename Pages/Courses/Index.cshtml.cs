using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    public SelectList CourseGroups { get; set; } = default!;

    public int TotalPages { get; set; }

    public async Task OnGetAsync()
    {
        const int pageSize = 10;
        CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name");

        var courses = await _cacheService.GetCoursesAsync();
        var filtered = courses.AsEnumerable();

        if (CourseGroupId.HasValue)
        {
            filtered = filtered.Where(c => c.CourseGroupId == CourseGroupId);
        }

        if (!string.IsNullOrWhiteSpace(SearchString))
        {
            var term = SearchString.Trim();
            filtered = filtered.Where(c =>
                c.Title?.Contains(term, StringComparison.InvariantCultureIgnoreCase) ?? false);
        }

        var ordered = filtered
            .OrderBy(c => c.Date)
            .ThenBy(c => c.Id)
            .ToList();

        TotalPages = (int)Math.Ceiling(ordered.Count / (double)pageSize);
        Courses = ordered
            .Skip((PageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
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
