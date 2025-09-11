using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseBlocks;

[Authorize(Roles = "Admin")]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;

    [BindProperty]
    public CourseBlock CourseBlock { get; set; } = default!;

    public DeleteModel(ApplicationDbContext context)
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

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var block = await _context.CourseBlocks
            .Include(b => b.Modules)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (block == null) return NotFound();
        foreach (var course in block.Modules)
        {
            course.CourseBlockId = null;
        }
        _context.CourseBlocks.Remove(block);
        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
