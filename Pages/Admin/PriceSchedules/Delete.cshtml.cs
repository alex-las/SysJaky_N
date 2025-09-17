using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.PriceSchedules;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DeleteModel(ApplicationDbContext context)
    {
        _context = context;
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
            return NotFound();
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
            return NotFound();
        }

        _context.PriceSchedules.Remove(schedule);
        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
