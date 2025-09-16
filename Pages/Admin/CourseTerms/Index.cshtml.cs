using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseTerms;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IList<CourseTerm> Terms { get; set; } = new List<CourseTerm>();

    public List<SelectListItem> CourseOptions { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? CourseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool OnlyActive { get; set; }

    public async Task OnGetAsync()
    {
        var courseQuery = _context.Courses
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .Select(c => new SelectListItem(c.Title, c.Id.ToString(), CourseId == c.Id));

        CourseOptions = await courseQuery.ToListAsync();

        var termQuery = _context.CourseTerms
            .AsNoTracking()
            .Include(t => t.Course)
            .Include(t => t.Instructor)
            .AsQueryable();

        if (CourseId.HasValue)
        {
            termQuery = termQuery.Where(t => t.CourseId == CourseId);
        }

        if (OnlyActive)
        {
            termQuery = termQuery.Where(t => t.IsActive);
        }

        Terms = await termQuery
            .OrderByDescending(t => t.StartUtc)
            .ToListAsync();
    }
}
