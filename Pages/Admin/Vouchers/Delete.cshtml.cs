using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Vouchers;

[Authorize(Roles = "Admin")]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DeleteModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Voucher Voucher { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var voucher = await _context.Vouchers
            .Include(v => v.AppliesToCourse)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (voucher == null)
        {
            return NotFound();
        }
        Voucher = voucher;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var voucher = await _context.Vouchers.FindAsync(Voucher.Id);
        if (voucher != null)
        {
            _context.Vouchers.Remove(voucher);
            await _context.SaveChangesAsync();
        }
        return RedirectToPage("Index");
    }
}
