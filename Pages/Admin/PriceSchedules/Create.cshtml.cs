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
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;

    private readonly IStringLocalizer<CreateModel> _localizer;

    public CreateModel(ApplicationDbContext context, IStringLocalizer<CreateModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    [BindProperty]
    public PriceSchedule PriceSchedule { get; set; } = new();

    public List<SelectListItem> Courses { get; set; } = new();

    public List<PriceSchedule> ConflictingSchedules { get; private set; } = new();

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
            ModelState.AddModelError("PriceSchedule.ToUtc", _localizer["EndTimeMustFollowStart"]);
        }

        await DetectConflictsAsync(fromUtc, toUtc);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        PriceSchedule.FromUtc = fromUtc;
        PriceSchedule.ToUtc = toUtc;

        _context.PriceSchedules.Add(PriceSchedule);
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
            .Where(p => p.CourseId == PriceSchedule.CourseId)
            .Where(p => p.FromUtc < toUtc && fromUtc < p.ToUtc)
            .OrderBy(p => p.FromUtc)
            .ToListAsync();

        if (ConflictingSchedules.Count == 0)
        {
            return;
        }

        var suggestedEnd = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc).ToLocalTime();
        ModelState.AddModelError(nameof(PriceSchedule.FromUtc),
            $"Nový interval se překrývá s {ConflictingSchedules.Count} existujícími plány. Upravte jejich konec nejpozději na {suggestedEnd:g} nebo změňte termín.");
    }
}
