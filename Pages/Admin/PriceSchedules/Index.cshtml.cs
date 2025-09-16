using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.PriceSchedules;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IList<PriceSchedule> PriceSchedules { get; set; } = new List<PriceSchedule>();

    public async Task OnGetAsync()
    {
        PriceSchedules = await _context.PriceSchedules
            .Include(p => p.Course)
            .OrderBy(p => p.CourseId)
            .ThenBy(p => p.FromUtc)
            .ToListAsync();
    }
}
