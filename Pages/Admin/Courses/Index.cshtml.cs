using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Courses;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<IndexModel> _localizer;

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public IList<Course> Courses { get; set; } = new List<Course>();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int? CategoryId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchString { get; set; }

    public SelectList CourseCategories { get; set; } = default!;

    public int TotalPages { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = _localizer["Title"];
        const int pageSize = 10;
        CourseCategories = new SelectList(
            await _context.CourseCategories
                .AsNoTracking()
                .OrderBy(category => category.SortOrder)
                .ThenBy(category => category.Name)
                .Select(category => new { category.Id, category.Name })
                .ToListAsync(),
            "Id",
            "Name");

        var query = _context.Courses
            .Include(c => c.Categories)
            .AsQueryable();

        if (CategoryId.HasValue)
        {
            query = query.Where(c => c.Categories.Any(category => category.Id == CategoryId));
        }

        if (!string.IsNullOrWhiteSpace(SearchString))
        {
            var pattern = $"%{SearchString.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.Title, pattern));
        }

        var orderedQuery = query.OrderBy(c => c.Date);

        var count = await orderedQuery.CountAsync();
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        Courses = await orderedQuery
            .Skip((PageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}

