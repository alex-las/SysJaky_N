using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SysJaky_N.Authorization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Orders;

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
    public Order Order { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound(_localizer["OrderNotFound"]);
        Order = order;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var order = await _context.Orders.FindAsync(Order.Id);
        if (order == null) return NotFound(_localizer["OrderNotFound"]);
        order.Status = Order.Status;
        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
