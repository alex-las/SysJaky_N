using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Courses;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
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
        var query = _context.Courses
            .Include(c => c.CourseGroup)
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

        query = query.OrderBy(c => c.Date);

        var count = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        Courses = await query.Skip((PageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
    }
}

