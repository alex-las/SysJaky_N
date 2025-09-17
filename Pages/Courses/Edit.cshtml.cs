using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using System.Security.Claims;

namespace SysJaky_N.Pages.Courses;

[Authorize(Roles = "Admin")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICacheService _cacheService;

    public EditModel(ApplicationDbContext context, IAuditService auditService, ICacheService cacheService)
    {
        _context = context;
        _auditService = auditService;
        _cacheService = cacheService;
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
            _cacheService.RemoveCourseList();
            _cacheService.RemoveCourseDetail(Course.Id);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _auditService.LogAsync(userId, "CourseEdited", $"Course {Course.Id} edited");
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
