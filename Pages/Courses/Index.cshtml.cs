using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Courses;

[Authorize(Roles = "Admin")]
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

    public int TotalPages { get; set; }

    public async Task OnGetAsync()
    {
        const int pageSize = 10;
        var query = _context.Courses.OrderBy(c => c.Date);
        var count = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        Courses = await query.Skip((PageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
    }
}
