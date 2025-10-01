using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Hubs;

[Authorize(Policy = AuthorizationPolicies.AdminDashboardAccess)]
public class DashboardHub : Hub
{
    private readonly ApplicationDbContext _context;

    public DashboardHub(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RealTimeStatsMessage> RequestRealtimeStats(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var onlineSince = now.AddMinutes(-15);

        var onlineUsers = await _context.LogEntries
            .AsNoTracking()
            .Where(log => log.Timestamp >= onlineSince)
            .Select(log => log.CorrelationId ?? log.Id.ToString())
            .Distinct()
            .CountAsync(cancellationToken);

        var cartWindow = now.AddHours(-2);
        var carts = await _context.Orders
            .AsNoTracking()
            .Where(order => order.Status == OrderStatus.Pending && order.CreatedAt >= cartWindow)
            .Select(order => new
            {
                order.Total,
                ItemCount = order.Items.Sum(item => item.Quantity)
            })
            .ToListAsync(cancellationToken);

        var activeCarts = carts.Count;
        var totalValue = carts.Sum(cart => cart.Total);
        var totalItems = carts.Sum(cart => cart.ItemCount);

        return new RealTimeStatsMessage(
            onlineUsers,
            activeCarts,
            Math.Round(totalValue, 2),
            totalItems,
            DateTime.UtcNow);
    }

    public sealed record RealTimeStatsMessage(
        int OnlineUzivatelu,
        int AktivniKosiky,
        decimal CelkovaHodnota,
        int PolozkyCelkem,
        DateTime VytvorenoUtc);
}
