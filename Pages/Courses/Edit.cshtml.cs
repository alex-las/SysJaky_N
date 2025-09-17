using Microsoft.AspNetCore.Authorization;
using System;
using System.IO;
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
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICourseMediaStorage _courseMediaStorage;
    private readonly ICacheService _cacheService;

    public EditModel(
        ApplicationDbContext context,
        IAuditService auditService,
        ICourseMediaStorage courseMediaStorage,
        ICacheService cacheService)
    {
        _context = context;
        _auditService = auditService;
        _courseMediaStorage = courseMediaStorage;
        _cacheService = cacheService;
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
        if (CoverImage is { Length: > 0 } && !string.Equals(CoverImage.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(CoverImage), "Nahrajte prosím obálku ve formátu JPEG.");
        }
        else if (CoverImage is { Length: 0 })
        {
            ModelState.AddModelError(nameof(CoverImage), "Soubor s obálkou je prázdný.");
        }

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
        courseToUpdate.CourseGroupId = Course.CourseGroupId;
        courseToUpdate.Price = Course.Price;
        courseToUpdate.Date = Course.Date;

        try
        {
            if (CoverImage is { Length: > 0 })
            {
                try
                {
                    using var imageStream = CoverImage.OpenReadStream();
                    var coverUrl = await _courseMediaStorage.SaveCoverImageAsync(
                        courseToUpdate.Id,
                        imageStream,
                        CoverImage.ContentType,
                        HttpContext.RequestAborted);
                    courseToUpdate.CoverImageUrl = coverUrl;
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException)
                {
                    ModelState.AddModelError(nameof(CoverImage), "Nepodařilo se uložit obálku kurzu. Zkontrolujte prosím soubor a zkuste to znovu.");
                    Course.CoverImageUrl = courseToUpdate.CoverImageUrl;
                    CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name", Course.CourseGroupId);
                    return Page();
                }
            }

            await _context.SaveChangesAsync();
            _cacheService.InvalidateCourseList();
            _cacheService.InvalidateCourseDetail(courseToUpdate.Id);
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
