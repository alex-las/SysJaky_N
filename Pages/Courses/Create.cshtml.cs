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
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICourseMediaStorage _courseMediaStorage;

    public CreateModel(ApplicationDbContext context, IAuditService auditService, ICourseMediaStorage courseMediaStorage)
    {
        _context = context;
        _auditService = auditService;
        _courseMediaStorage = courseMediaStorage;
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
        if (CoverImage is { Length: > 0 } && !string.Equals(CoverImage.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(CoverImage), "Nahrajte prosím obálku ve formátu JPEG.");
        }
        else if (CoverImage is { Length: 0 })
        {
            ModelState.AddModelError(nameof(CoverImage), "Soubor s obálkou je prázdný.");
        }

        if (!ModelState.IsValid)
        {
            CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name");
            return Page();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _context.Courses.Add(Course);
            await _context.SaveChangesAsync();

            if (CoverImage is { Length: > 0 })
            {
                using var imageStream = CoverImage.OpenReadStream();
                var coverUrl = await _courseMediaStorage.SaveCoverImageAsync(
                    Course.Id,
                    imageStream,
                    CoverImage.ContentType,
                    HttpContext.RequestAborted);
                Course.CoverImageUrl = coverUrl;
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            await transaction.RollbackAsync();
            Course.Id = 0;
            ModelState.AddModelError(nameof(CoverImage), "Nepodařilo se uložit obálku kurzu. Zkontrolujte prosím soubor a zkuste to znovu.");
            CourseGroups = new SelectList(_context.CourseGroups, "Id", "Name");
            return Page();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _auditService.LogAsync(userId, "CourseCreated", $"Course {Course.Id} created");
        return RedirectToPage("Index");
    }
}
