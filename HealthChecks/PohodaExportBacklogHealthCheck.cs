using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.HealthChecks;

public sealed class PohodaExportBacklogHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public PohodaExportBacklogHealthCheck(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        TimeProvider timeProvider)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var pendingJobs = await dbContext.PohodaExportJobs
            .AsNoTracking()
            .Where(job => job.Status == PohodaExportJobStatus.Pending)
            .Select(job => new { job.CreatedAtUtc, job.NextAttemptAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var failedJobs = await dbContext.PohodaExportJobs
            .AsNoTracking()
            .Where(job => job.Status == PohodaExportJobStatus.Failed)
            .Select(job => job.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var pendingCount = pendingJobs.Count;
        var failedCount = failedJobs.Count;
        var oldestPending = pendingJobs.Count > 0
            ? pendingJobs.Min(job => job.CreatedAtUtc)
            : (DateTime?)null;
        var nextAttemptDue = pendingJobs.Count > 0
            ? pendingJobs.Min(job => job.NextAttemptAtUtc ?? job.CreatedAtUtc)
            : (DateTime?)null;

        var data = new Dictionary<string, object>
        {
            ["pendingJobs"] = pendingCount,
            ["failedJobs"] = failedCount,
            ["oldestPendingUtc"] = oldestPending?.ToString("O") ?? "n/a",
            ["nextAttemptUtc"] = nextAttemptDue?.ToString("O") ?? "n/a",
            ["checkedAtUtc"] = now.ToString("O")
        };

        if (failedCount > 0)
        {
            return HealthCheckResult.Unhealthy(
                "One or more Pohoda export jobs are in a failed state.",
                data: data);
        }

        if (pendingCount > 0)
        {
            return HealthCheckResult.Degraded(
                "There are pending Pohoda export jobs waiting to be processed.",
                data: data);
        }

        return HealthCheckResult.Healthy(
            "Pohoda export job queue is empty.",
            data: data);
    }
}
