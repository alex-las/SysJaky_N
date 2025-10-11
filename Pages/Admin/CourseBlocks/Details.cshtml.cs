using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseBlocks;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    private readonly IStringLocalizer<DetailsModel> _localizer;

    public CourseBlock CourseBlock { get; set; } = default!;

    public DetailsModel(ApplicationDbContext context, IStringLocalizer<DetailsModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var block = await _context.CourseBlocks
            .Include(b => b.Modules)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (block == null) return NotFound(_localizer["CourseBlockNotFound"]);
        CourseBlock = block;
        return Page();
    }
}
