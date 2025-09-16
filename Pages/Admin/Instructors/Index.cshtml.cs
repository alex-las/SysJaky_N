using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using InstructorModel = SysJaky_N.Models.Instructor;

namespace SysJaky_N.Pages.Admin.Instructors;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IList<InstructorModel> Instructors { get; set; } = new List<InstructorModel>();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public async Task OnGetAsync()
    {
        var query = _context.Instructors
            .AsNoTracking()
            .Include(i => i.CourseTerms)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = $"%{Search.Trim()}%";
            query = query.Where(i => EF.Functions.Like(i.FullName, term) || (i.Email != null && EF.Functions.Like(i.Email, term)));
        }

        Instructors = await query
            .OrderBy(i => i.FullName)
            .ToListAsync();
    }
}
