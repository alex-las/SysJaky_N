using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Courses;

[Authorize(Roles = "Admin")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public EditModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    [BindProperty]
    public Course Course { get; set; } = new();

    public SelectList CourseGroups { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Course? course = await _context.Courses.FindAsync(id);
        if (course == null)
        {
            return NotFound();
        }
        Course = course;
        CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name");
            return Page();
        }

        _context.Attach(Course).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
            var userId = _userManager.GetUserId(User) ?? string.Empty;
            await _auditService.LogAsync(userId, "EditCourse", $"CourseId:{Course.Id}");
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.Courses.AnyAsync(e => e.Id == Course.Id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return RedirectToPage("Index");
    }
}
