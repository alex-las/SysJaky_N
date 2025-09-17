using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Courses;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DeleteModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Course Course { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Course? course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (course == null)
        {
            return NotFound();
        }
        Course = course;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        Course? course = await _context.Courses.FindAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
