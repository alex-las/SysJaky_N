using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SysJaky_N.Data;

namespace SysJaky_N.Services;

public class DataRetentionOptions
{
    public int ExecutionIntervalHours { get; set; } = 24;
    public int LogRetentionDays { get; set; } = 30;
    public int EmailLogRetentionDays { get; set; } = 180;
    public int ContactMessageRetentionDays { get; set; } = 365;
    public int AuditLogRetentionDays { get; set; } = 365;
    public int WaitlistEntryRetentionDays { get; set; } = 90;
}

public class DataRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataRetentionService> _logger;
    private readonly DataRetentionOptions _options;
    private readonly TimeSpan _interval;

    public DataRetentionService(
        IServiceScopeFactory scopeFactory,
        IOptions<DataRetentionOptions> options,
        ILogger<DataRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value ?? new DataRetentionOptions();

        var intervalHours = _options.ExecutionIntervalHours > 0 ? _options.ExecutionIntervalHours : 24;
        _interval = TimeSpan.FromHours(intervalHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ApplyRetentionPoliciesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chyba při čištění dat podle retenční politiky.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ApplyRetentionPoliciesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var nowUtc = DateTime.UtcNow;

        var totalRemoved = 0;

        if (_options.LogRetentionDays > 0)
        {
            var cutoff = nowUtc.AddDays(-_options.LogRetentionDays);
            totalRemoved += await context.LogEntries
                .Where(entry => entry.Timestamp < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (_options.EmailLogRetentionDays > 0)
        {
            var cutoff = nowUtc.AddDays(-_options.EmailLogRetentionDays);
            totalRemoved += await context.EmailLogs
                .Where(entry => entry.SentUtc < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (_options.AuditLogRetentionDays > 0)
        {
            var cutoff = nowUtc.AddDays(-_options.AuditLogRetentionDays);
            totalRemoved += await context.AuditLogs
                .Where(entry => entry.Timestamp < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (_options.ContactMessageRetentionDays > 0)
        {
            var cutoff = nowUtc.AddDays(-_options.ContactMessageRetentionDays);
            totalRemoved += await context.ContactMessages
                .Where(message => message.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (_options.WaitlistEntryRetentionDays > 0)
        {
            var cutoff = nowUtc.AddDays(-_options.WaitlistEntryRetentionDays);
            totalRemoved += await context.WaitlistEntries
                .Where(entry => entry.CreatedAtUtc < cutoff && (!entry.ReservationExpiresAtUtc.HasValue || entry.ReservationExpiresAtUtc < nowUtc))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (totalRemoved > 0)
        {
            _logger.LogInformation("Retenční politika odstranila {RemovedCount} starých záznamů.", totalRemoved);
        }
    }
}
