using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseBlocks;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public CourseBlock CourseBlock { get; set; } = default!;

    public DetailsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var block = await _context.CourseBlocks
            .Include(b => b.Modules)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (block == null) return NotFound();
        CourseBlock = block;
        return Page();
    }
}
