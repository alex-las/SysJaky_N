using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using InstructorModel = SysJaky_N.Models.Instructor;

namespace SysJaky_N.Pages.Admin.Instructors;

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

    public IList<InstructorModel> Instructors { get; set; } = new List<InstructorModel>();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public string PageTitle => _localizer["Title"];

    public async Task OnGetAsync()
    {
        ViewData["Title"] = PageTitle;

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
