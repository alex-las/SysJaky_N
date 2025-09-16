using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;

namespace SysJaky_N.Pages.Admin.Dashboard;

[Authorize(Roles = "Admin")]
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

    public async Task OnGetAsync()
    {
        OrderCount = await _context.Orders.CountAsync();
        TotalRevenue = await _context.Orders.SumAsync(o => o.TotalPrice);

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

        var revenue = await _context.Orders
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Sum(o => o.TotalPrice)
            })
            .OrderBy(g => g.Year)
            .ThenBy(g => g.Month)
            .ToListAsync();

        foreach (var item in revenue)
        {
            var period = new DateTime(item.Year, item.Month, 1);
            RevenueLabels.Add(period.ToString("yyyy-MM"));
            RevenueValues.Add(item.Total);
        }
    }
}
