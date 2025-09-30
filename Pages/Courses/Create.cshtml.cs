using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using System.Security.Claims;

namespace SysJaky_N.Pages.Courses;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICourseEditor _courseEditor;

    public CreateModel(
        ApplicationDbContext context,
        IAuditService auditService,
        ICourseEditor courseEditor)
    {
        _context = context;
        _auditService = auditService;
        _courseEditor = courseEditor;
    }

    [BindProperty]
    public Course Course { get; set; } = new();

    [BindProperty]
    public IFormFile? CoverImage { get; set; }

    public SelectList CourseGroups { get; set; } = default!;

    public void OnGet()
    {
        CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        _courseEditor.ValidateCoverImage(CoverImage, ModelState, nameof(CoverImage));

        if (!ModelState.IsValid)
        {
            CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name", Course.CourseGroupId);
            return Page();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        _context.Courses.Add(Course);
        await _context.SaveChangesAsync();

        var coverResult = await _courseEditor.SaveCoverImageAsync(
            Course.Id,
            CoverImage,
            HttpContext.RequestAborted);

        if (!coverResult.Succeeded)
        {
            await transaction.RollbackAsync();
            Course.Id = 0;
            ModelState.AddModelError(nameof(CoverImage), coverResult.ErrorMessage ?? string.Empty);
            CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name", Course.CourseGroupId);
            return Page();
        }

        if (coverResult.CoverImageUrl is { } coverUrl)
        {
            Course.CoverImageUrl = coverUrl;
            await _context.SaveChangesAsync();
        }

        await transaction.CommitAsync();

        _courseEditor.InvalidateCourseCache(Course.Id);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _auditService.LogAsync(userId, "CourseCreated", $"Course {Course.Id} created");
        return RedirectToPage("Index");
    }
}
