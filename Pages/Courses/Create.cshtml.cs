using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Courses;

[Authorize(Roles = "Admin")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public CreateModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
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
        var userId = _userManager.GetUserId(User) ?? string.Empty;
        await _auditService.LogAsync(userId, "CreateCourse", $"CourseId:{Course.Id}");
        return RedirectToPage("Index");
    }
}
