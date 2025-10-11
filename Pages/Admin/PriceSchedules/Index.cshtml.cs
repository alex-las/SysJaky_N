using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.PriceSchedules;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    protected IStringLocalizer<IndexModel> Localizer { get; }

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        Localizer = localizer;
    }

    public IList<PriceSchedule> PriceSchedules { get; set; } = new List<PriceSchedule>();

    public async Task OnGetAsync()
    {
        _ = Localizer["IndexPageName"];
        PriceSchedules = await _context.PriceSchedules
            .AsNoTracking()
            .Include(p => p.Course)
            .OrderBy(p => p.CourseId)
            .ThenBy(p => p.FromUtc)
            .ToListAsync();
    }
}
