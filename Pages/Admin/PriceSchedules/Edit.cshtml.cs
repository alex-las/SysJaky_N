using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.PriceSchedules;

[Authorize(Roles = "Admin")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public EditModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public PriceSchedule PriceSchedule { get; set; } = default!;

    public List<SelectListItem> Courses { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var schedule = await _context.PriceSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (schedule == null)
        {
            return NotFound();
        }

        if (schedule.FromUtc != default)
        {
            schedule.FromUtc = DateTime.SpecifyKind(schedule.FromUtc, DateTimeKind.Utc).ToLocalTime();
        }

        if (schedule.ToUtc != default)
        {
            schedule.ToUtc = DateTime.SpecifyKind(schedule.ToUtc, DateTimeKind.Utc).ToLocalTime();
        }

        PriceSchedule = schedule;
        await LoadCoursesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCoursesAsync();

        var fromUtc = DateTime.SpecifyKind(PriceSchedule.FromUtc, DateTimeKind.Local).ToUniversalTime();
        var toUtc = DateTime.SpecifyKind(PriceSchedule.ToUtc, DateTimeKind.Local).ToUniversalTime();

        if (toUtc <= fromUtc)
        {
            ModelState.AddModelError("PriceSchedule.ToUtc", "End time must be after start time.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var schedule = await _context.PriceSchedules.FindAsync(PriceSchedule.Id);
        if (schedule == null)
        {
            return NotFound();
        }

        schedule.CourseId = PriceSchedule.CourseId;
        schedule.FromUtc = fromUtc;
        schedule.ToUtc = toUtc;
        schedule.NewPriceExcl = PriceSchedule.NewPriceExcl;

        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task LoadCoursesAsync()
    {
        Courses = await _context.Courses
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .Select(c => new SelectListItem(c.Title, c.Id.ToString(), c.Id == PriceSchedule.CourseId))
            .ToListAsync();
    }
}
