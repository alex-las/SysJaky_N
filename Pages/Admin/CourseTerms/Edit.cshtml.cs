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
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Admin.CourseTerms;

[Authorize(Roles = "Admin")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public EditModel(ApplicationDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    [BindProperty]
    public CourseTermInputModel Input { get; set; } = new();

    public List<SelectListItem> CourseOptions { get; set; } = new();

    public List<SelectListItem> InstructorOptions { get; set; } = new();

    public int SeatsTaken { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var term = await _context.CourseTerms
            .AsNoTracking()
            .Include(t => t.Course)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (term == null)
        {
            return NotFound();
        }

        Input = new CourseTermInputModel
        {
            CourseId = term.CourseId,
            StartUtc = DateTime.SpecifyKind(term.StartUtc, DateTimeKind.Utc).ToLocalTime(),
            EndUtc = DateTime.SpecifyKind(term.EndUtc, DateTimeKind.Utc).ToLocalTime(),
            Capacity = term.Capacity,
            IsActive = term.IsActive,
            InstructorId = term.InstructorId
        };
        SeatsTaken = term.SeatsTaken;

        await LoadSelectListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        await LoadSelectListsAsync();

        var term = await _context.CourseTerms
            .FirstOrDefaultAsync(t => t.Id == id);

        if (term == null)
        {
            return NotFound();
        }

        SeatsTaken = term.SeatsTaken;
        var originalCourseId = term.CourseId;

        var startUtc = DateTime.SpecifyKind(Input.StartUtc, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(Input.EndUtc, DateTimeKind.Local).ToUniversalTime();

        if (endUtc <= startUtc)
        {
            ModelState.AddModelError("Input.EndUtc", "End time must be after start time.");
        }

        if (Input.Capacity < term.SeatsTaken)
        {
            ModelState.AddModelError("Input.Capacity", $"Capacity cannot be less than the current seats taken ({term.SeatsTaken}).");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        term.CourseId = Input.CourseId;
        term.StartUtc = startUtc;
        term.EndUtc = endUtc;
        term.Capacity = Input.Capacity;
        term.IsActive = Input.IsActive;
        term.InstructorId = Input.InstructorId;

        await _context.SaveChangesAsync();
        _cacheService.RemoveCourseDetail(originalCourseId);
        if (originalCourseId != term.CourseId)
        {
            _cacheService.RemoveCourseDetail(term.CourseId);
        }
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
