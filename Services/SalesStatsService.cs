using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class SalesStatsService : ScopedRecurringBackgroundService<SalesStatsService>
{
    public SalesStatsService(IServiceScopeFactory scopeFactory, ILogger<SalesStatsService> logger)
        : base(scopeFactory, logger, RecurringSchedule.DailyAtUtc(TimeSpan.Zero))
    {
    }

    protected override string FailureMessage => "Error updating sales statistics";

    protected override async Task ExecuteInScopeAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        await UpdateSalesStatsAsync(serviceProvider, stoppingToken);
    }

    private static async Task UpdateSalesStatsAsync(IServiceProvider serviceProvider, CancellationToken token)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        var salesByDate = await context.Orders
            .Where(o => o.Status == OrderStatus.Paid)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Revenue = g.Sum(o => o.Total),
                OrderCount = g.Count(),
                AverageOrderValue = g.Average(o => o.Total)
            })
            .ToListAsync(token);

        var existingStats = await context.SalesStats
            .ToDictionaryAsync(s => s.Date, token);

        foreach (var stat in salesByDate)
        {
            var date = DateOnly.FromDateTime(stat.Date);
            if (existingStats.TryGetValue(date, out var entity))
            {
                entity.Revenue = stat.Revenue;
                entity.OrderCount = stat.OrderCount;
                entity.AverageOrderValue = stat.AverageOrderValue;
            }
            else
            {
                context.SalesStats.Add(new SalesStat
                {
                    Date = date,
                    Revenue = stat.Revenue,
                    OrderCount = stat.OrderCount,
                    AverageOrderValue = stat.AverageOrderValue
                });
            }
        }

        await context.SaveChangesAsync(token);
    }
}
