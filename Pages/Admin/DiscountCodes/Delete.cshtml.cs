using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.DiscountCodes;

[Authorize(Roles = "Admin")]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DeleteModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public DiscountCode DiscountCode { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var discount = await _context.DiscountCodes.FindAsync(id);
        if (discount == null)
        {
            return NotFound();
        }
        DiscountCode = discount;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var discount = await _context.DiscountCodes.FindAsync(DiscountCode.Id);
        if (discount != null)
        {
            _context.DiscountCodes.Remove(discount);
            await _context.SaveChangesAsync();
        }
        return RedirectToPage("Index");
    }
}
