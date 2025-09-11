using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;
using System.Security.Claims;

namespace SysJaky_N.Pages.Account;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DashboardModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<OrderItem> UpcomingItems { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return;

        UpcomingItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Course)
            .Where(oi => oi.Order != null
                && oi.Order.UserId == userId
                && oi.Course != null
                && oi.Course.Date >= DateTime.Today)
            .ToListAsync();
    }
}
