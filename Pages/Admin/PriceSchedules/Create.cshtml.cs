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
        await LoadCoursesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCoursesAsync();

        if (PriceSchedule.ToUtc <= PriceSchedule.FromUtc)
        {
            ModelState.AddModelError("PriceSchedule.ToUtc", "End time must be after start time.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        _context.PriceSchedules.Add(PriceSchedule);
        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task LoadCoursesAsync()
    {
        Courses = await _context.Courses
            .OrderBy(c => c.Title)
            .Select(c => new SelectListItem(c.Title, c.Id.ToString(), c.Id == PriceSchedule.CourseId))
            .ToListAsync();
    }
}
