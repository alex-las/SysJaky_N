using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseTerms;

[Authorize(Roles = "Admin")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public CreateModel(ApplicationDbContext context)
    {
        _context = context;
        Input.StartUtc = DateTime.UtcNow.ToLocalTime();
        Input.EndUtc = Input.StartUtc.AddHours(1);
    }

    [BindProperty]
    public CourseTermInputModel Input { get; set; } = new();

    public List<SelectListItem> CourseOptions { get; set; } = new();

    public List<SelectListItem> InstructorOptions { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadSelectListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadSelectListsAsync();

        var startUtc = DateTime.SpecifyKind(Input.StartUtc, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(Input.EndUtc, DateTimeKind.Local).ToUniversalTime();

        if (endUtc <= startUtc)
        {
            ModelState.AddModelError("Input.EndUtc", "End time must be after start time.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var term = new CourseTerm
        {
            CourseId = Input.CourseId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Capacity = Input.Capacity,
            SeatsTaken = 0,
            IsActive = Input.IsActive,
            InstructorId = Input.InstructorId
        };

        _context.CourseTerms.Add(term);
        await _context.SaveChangesAsync();

        return RedirectToPage("Index");
    }

    private async Task LoadSelectListsAsync()
    {
        CourseOptions = await _context.Courses
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .Select(c => new SelectListItem(c.Title, c.Id.ToString(), Input.CourseId == c.Id))
            .ToListAsync();

        var instructorItems = await _context.Instructors
            .AsNoTracking()
            .OrderBy(i => i.FullName)
            .Select(i => new SelectListItem(i.FullName, i.Id.ToString(), Input.InstructorId == i.Id))
            .ToListAsync();

        InstructorOptions = new List<SelectListItem>
        {
            new("Unassigned", string.Empty, Input.InstructorId == null)
        };
        InstructorOptions.AddRange(instructorItems);
    }
}
