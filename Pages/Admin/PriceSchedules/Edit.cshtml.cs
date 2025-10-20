using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.PriceSchedules;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    private readonly IStringLocalizer<EditModel> _localizer;

    public EditModel(ApplicationDbContext context, IStringLocalizer<EditModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    [BindProperty]
    public PriceSchedule PriceSchedule { get; set; } = default!;

    public List<SelectListItem> Courses { get; set; } = new();

    public List<PriceSchedule> ConflictingSchedules { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var schedule = await _context.PriceSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (schedule == null)
        {
            return NotFound(_localizer["PriceScheduleNotFound"]);
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
            ModelState.AddModelError("PriceSchedule.ToUtc", _localizer["EndTimeMustFollowStart"]);
        }

        await DetectConflictsAsync(fromUtc, toUtc);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var schedule = await _context.PriceSchedules.FindAsync(PriceSchedule.Id);
        if (schedule == null)
        {
            return NotFound(_localizer["PriceScheduleNotFound"]);
        }

        schedule.CourseId = PriceSchedule.CourseId;
        schedule.FromUtc = fromUtc;
        schedule.ToUtc = toUtc;
        schedule.NewPriceExcl = PriceSchedule.NewPriceExcl;

        await _context.SaveChangesAsync();
        return RedirectToPage(_localizer["IndexPageName"]);
    }

    private async Task LoadCoursesAsync()
    {
        Courses = await _context.Courses
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .Select(c => new SelectListItem(c.Title, c.Id.ToString(), c.Id == PriceSchedule.CourseId))
            .ToListAsync();
    }

    private async Task DetectConflictsAsync(DateTime fromUtc, DateTime toUtc)
    {
        ConflictingSchedules = await _context.PriceSchedules
            .AsNoTracking()
            .Include(p => p.Course)
            .Where(p => p.CourseId == PriceSchedule.CourseId && p.Id != PriceSchedule.Id)
            .Where(p => p.FromUtc < toUtc && fromUtc < p.ToUtc)
            .OrderBy(p => p.FromUtc)
            .ToListAsync();

        if (ConflictingSchedules.Count == 0)
        {
            return;
        }

        var suggestedEnd = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc).ToLocalTime();
        ModelState.AddModelError(nameof(PriceSchedule.FromUtc),
            $"Upravený interval se překrývá s {ConflictingSchedules.Count} existujícími plány. Ukončete je nejpozději k {suggestedEnd:g} nebo zvolte jiný termín.");
    }
}
