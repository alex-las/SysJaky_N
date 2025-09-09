using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;
using System.Security.Claims;

namespace SysJaky_N.Pages.Orders;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DetailsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public Order Order { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Course)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!User.IsInRole("Admin") && order.UserId != userId)
            return Forbid();

        Order = order;
        return Page();
    }
}
