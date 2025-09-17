using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using System.Security.Claims;

namespace SysJaky_N.Pages.Courses;

[Authorize(Roles = "Admin")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICacheService _cacheService;

    public CreateModel(ApplicationDbContext context, IAuditService auditService, ICacheService cacheService)
    {
        _context = context;
        _auditService = auditService;
        _cacheService = cacheService;
    }

    [BindProperty]
    public Course Course { get; set; } = new();

    public SelectList CourseGroups { get; set; } = default!;

    public void OnGet()
    {
        CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name");
            return Page();
        }

        _context.Courses.Add(Course);
        await _context.SaveChangesAsync();
        _cacheService.RemoveCourseList();
        _cacheService.RemoveCourseDetail(Course.Id);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _auditService.LogAsync(userId, "CourseCreated", $"Course {Course.Id} created");
        return RedirectToPage("Index");
    }
}
