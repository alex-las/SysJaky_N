using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class SalesStatsService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SalesStatsService> _logger;

    public SalesStatsService(IServiceScopeFactory scopeFactory, ILogger<SalesStatsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateSalesStatsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sales statistics");
            }

            try
            {
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1);
                var delay = nextRun - now;
                if (delay <= TimeSpan.Zero)
                {
                    delay = TimeSpan.FromDays(1);
                }

                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
        }
    }

    private async Task UpdateSalesStatsAsync(CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

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
