using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Orders;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public EditModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Order Order { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();
        Order = order;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var order = await _context.Orders.FindAsync(Order.Id);
        if (order == null) return NotFound();
        order.Status = Order.Status;
        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
