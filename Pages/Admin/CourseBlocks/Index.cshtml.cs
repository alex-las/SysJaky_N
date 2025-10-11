using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseBlocks;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    protected IStringLocalizer<IndexModel> Localizer { get; }

    public IList<CourseBlock> CourseBlocks { get; set; } = new List<CourseBlock>();

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        Localizer = localizer;
    }

    public async Task OnGetAsync()
    {
        _ = Localizer["IndexPageName"];
        CourseBlocks = await _context.CourseBlocks
            .Include(b => b.Modules)
            .ToListAsync();
    }
}
