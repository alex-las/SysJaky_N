using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Orders;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public IList<Order> Orders { get; set; } = new List<Order>();

    public async Task OnGetAsync()
    {
        var query = _context.Orders.Include(o => o.Items);
        if (!User.IsInRole("Admin"))
        {
            var userId = _userManager.GetUserId(User);
            query = query.Where(o => o.UserId == userId);
        }
        Orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }
}
