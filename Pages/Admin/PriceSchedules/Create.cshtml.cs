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

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public CreateModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public PriceSchedule PriceSchedule { get; set; } = new();

    public List<SelectListItem> Courses { get; set; } = new();

    public async Task OnGetAsync()
    {
        PriceSchedule.FromUtc = DateTime.UtcNow.ToLocalTime();
        PriceSchedule.ToUtc = PriceSchedule.FromUtc.AddDays(7);
        await LoadCoursesAsync();
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

        PriceSchedule.FromUtc = fromUtc;
        PriceSchedule.ToUtc = toUtc;

        _context.PriceSchedules.Add(PriceSchedule);
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
