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
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public EditModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Voucher Voucher { get; set; } = default!;

    public List<SelectListItem> Courses { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var voucher = await _context.Vouchers.FindAsync(id);
        if (voucher == null)
        {
            return NotFound();
        }

        Voucher = voucher;
        await LoadCoursesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadCoursesAsync();
            return Page();
        }

        var existing = await _context.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Id == Voucher.Id);
        if (existing == null)
        {
            return NotFound();
        }

        Voucher.UsedCount = existing.UsedCount;
        _context.Attach(Voucher).State = EntityState.Modified;
        _context.Entry(Voucher).Property(v => v.UsedCount).IsModified = false;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.Vouchers.AnyAsync(v => v.Id == Voucher.Id))
            {
                return NotFound();
            }

            throw;
        }

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
