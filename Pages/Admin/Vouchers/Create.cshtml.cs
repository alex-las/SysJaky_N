using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Vouchers;

[Authorize(Roles = "Admin")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public CreateModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Voucher Voucher { get; set; } = new();

    public List<SelectListItem> Courses { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadCoursesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCoursesAsync();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        _context.Vouchers.Add(Voucher);
        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task LoadCoursesAsync()
    {
        var courseItems = await _context.Courses
            .OrderBy(c => c.Title)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title,
                Selected = Voucher.AppliesToCourseId == c.Id
            })
            .ToListAsync();

        Courses = new List<SelectListItem>
        {
            new SelectListItem("All courses", string.Empty, Voucher.AppliesToCourseId == null)
        };
        Courses.AddRange(courseItems);
    }
}
