using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Authorization;
using SysJaky_N.Data;

namespace SysJaky_N.Pages.Admin.Dashboard;

[Authorize(Policy = AuthorizationPolicies.AdminDashboardAccess)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public int OrderCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public List<string> TopCourseLabels { get; set; } = new();
    public List<int> TopCourseValues { get; set; } = new();
    public List<string> RevenueLabels { get; set; } = new();
    public List<decimal> RevenueValues { get; set; } = new();
    public List<int> OrderCounts { get; set; } = new();
    public List<decimal> AverageOrderValues { get; set; } = new();

    public async Task OnGetAsync()
    {
        OrderCount = await _context.Orders.CountAsync();
        TotalRevenue = await _context.Orders.SumAsync(o => o.Total);

        var topCourses = await _context.OrderItems
            .Include(oi => oi.Course)
            .GroupBy(oi => oi.Course!.Title)
            .Select(g => new
            {
                Course = g.Key,
                Quantity = g.Sum(oi => oi.Quantity)
            })
            .OrderByDescending(g => g.Quantity)
            .Take(5)
            .ToListAsync();

        foreach (var item in topCourses)
        {
            TopCourseLabels.Add(item.Course);
            TopCourseValues.Add(item.Quantity);
        }

        var dailyStats = await _context.SalesStats
            .OrderBy(s => s.Date)
            .ToListAsync();

        foreach (var stat in dailyStats)
        {
            RevenueLabels.Add(stat.Date.ToString("yyyy-MM-dd"));
            RevenueValues.Add(stat.Revenue);
            OrderCounts.Add(stat.OrderCount);
            AverageOrderValues.Add(stat.AverageOrderValue);
        }
    }
}
