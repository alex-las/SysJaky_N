using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using System.Security.Claims;

namespace SysJaky_N.Pages.Courses;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICourseEditor _courseEditor;
    private readonly IStringLocalizer<EditModel> _localizer;

    public IStringLocalizer<EditModel> Localizer => _localizer;

    public EditModel(
        ApplicationDbContext context,
        IAuditService auditService,
        ICourseEditor courseEditor,
        IStringLocalizer<EditModel> localizer)
    {
        _context = context;
        _auditService = auditService;
        _courseEditor = courseEditor;
        _localizer = localizer;
    }

    [BindProperty]
    public Course Course { get; set; } = new();

    [BindProperty]
    public IFormFile? CoverImage { get; set; }

    public SelectList CourseGroups { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Course? course = await _context.Courses.FindAsync(id);
        if (course == null)
        {
            return NotFound();
        }
        Course = course;
        CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name", Course.CourseGroupId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        _courseEditor.ValidateCoverImage(CoverImage, ModelState, nameof(CoverImage));

        Course? courseToUpdate = await _context.Courses.FirstOrDefaultAsync(c => c.Id == Course.Id);
        if (courseToUpdate == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            Course.CoverImageUrl = courseToUpdate.CoverImageUrl;
            CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name", Course.CourseGroupId);
            return Page();
        }

        courseToUpdate.Title = Course.Title;
        courseToUpdate.Description = Course.Description;
        courseToUpdate.MetaTitle = Course.MetaTitle;
        courseToUpdate.MetaDescription = Course.MetaDescription;
        courseToUpdate.OpenGraphImage = Course.OpenGraphImage;
        courseToUpdate.CourseGroupId = Course.CourseGroupId;
        courseToUpdate.Price = Course.Price;
        courseToUpdate.Date = Course.Date;
        courseToUpdate.Level = Course.Level;
        courseToUpdate.Mode = Course.Mode;
        courseToUpdate.Duration = Course.Duration;

        var coverResult = await _courseEditor.SaveCoverImageAsync(
            courseToUpdate.Id,
            CoverImage,
            HttpContext.RequestAborted);

        if (!coverResult.Succeeded)
        {
            ModelState.AddModelError(nameof(CoverImage), coverResult.ErrorMessage ?? string.Empty);
            Course.CoverImageUrl = courseToUpdate.CoverImageUrl;
            CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name", Course.CourseGroupId);
            return Page();
        }

        if (coverResult.CoverImageUrl is { } coverUrl)
        {
            courseToUpdate.CoverImageUrl = coverUrl;
        }

        try
        {
            await _context.SaveChangesAsync();
            _courseEditor.InvalidateCourseCache(courseToUpdate.Id);
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
