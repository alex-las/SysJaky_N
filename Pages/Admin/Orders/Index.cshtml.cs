using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Orders;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<IndexModel> _localizer;

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public IList<Order> Orders { get; set; } = new List<Order>();

    public async Task OnGetAsync()
    {
        ViewData["Title"] = _localizer["Title"];
        Orders = await _context.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }
}

