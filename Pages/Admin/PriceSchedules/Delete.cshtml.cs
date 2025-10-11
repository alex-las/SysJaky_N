using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.PriceSchedules;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;

    private readonly IStringLocalizer<DeleteModel> _localizer;

    public DeleteModel(ApplicationDbContext context, IStringLocalizer<DeleteModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    [BindProperty]
    public PriceSchedule PriceSchedule { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var schedule = await _context.PriceSchedules
            .Include(p => p.Course)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (schedule == null)
        {
            return NotFound(_localizer["PriceScheduleNotFound"]);
        }

        schedule.FromUtc = DateTime.SpecifyKind(schedule.FromUtc, DateTimeKind.Utc).ToLocalTime();
        schedule.ToUtc = DateTime.SpecifyKind(schedule.ToUtc, DateTimeKind.Utc).ToLocalTime();

        PriceSchedule = schedule;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var schedule = await _context.PriceSchedules.FindAsync(PriceSchedule.Id);
        if (schedule == null)
        {
            return NotFound(_localizer["PriceScheduleNotFound"]);
        }

        _context.PriceSchedules.Remove(schedule);
        await _context.SaveChangesAsync();
        return RedirectToPage(_localizer["IndexPageName"]);
    }
}
