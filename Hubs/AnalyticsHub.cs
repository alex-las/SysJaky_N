using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SysJaky_N.Authorization;
using SysJaky_N.Services.Analytics;
using System.Collections.Generic;

namespace SysJaky_N.Hubs;

[Authorize(Policy = AuthorizationPolicies.AdminDashboardAccess)]
public class AnalyticsHub : Hub
{
    private readonly DashboardAnalyticsService _analytics;

    public AnalyticsHub(DashboardAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    public async Task<RealtimeStatsDto> ZiskejOkamziteStatistiky(FiltrHubu? filtr, CancellationToken cancellationToken)
    {
        var filter = filtr?.ToFilter() ?? FiltrHubu.Vychozi();
        return await _analytics.GetRealtimeStatsAsync(filter, cancellationToken);
    }

    public class FiltrHubu
    {
        public DateTime? Od { get; set; }
        public DateTime? Do { get; set; }
        public List<int> Normy { get; set; } = new();
        public List<int> Mesta { get; set; } = new();

        public AnalyticsFilter ToFilter()
        {
            var dnes = DateOnly.FromDateTime(DateTime.UtcNow);
            var od = Od.HasValue ? DateOnly.FromDateTime(Od.Value.ToUniversalTime()) : dnes.AddDays(-29);
            var doDatum = Do.HasValue ? DateOnly.FromDateTime(Do.Value.ToUniversalTime()) : dnes;

            if (od > doDatum)
            {
                (od, doDatum) = (doDatum, od);
            }

            return new AnalyticsFilter(
                od,
                doDatum,
                Normy?.Distinct().ToList() ?? new List<int>(),
                Mesta?.Distinct().ToList() ?? new List<int>());
        }

        public static AnalyticsFilter Vychozi()
        {
            var dnes = DateOnly.FromDateTime(DateTime.UtcNow);
            return new AnalyticsFilter(dnes.AddDays(-29), dnes, new List<int>(), new List<int>());
        }
    }
}
