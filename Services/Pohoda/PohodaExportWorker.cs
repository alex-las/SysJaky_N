using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SysJaky_N.Data;
using SysJaky_N.Services;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaExportWorker : ScopedRecurringBackgroundService<PohodaExportWorker>
{
    private readonly ILogger<PohodaExportWorker> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IPohodaSqlOptions _options;

    public PohodaExportWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PohodaExportWorker> logger,
        IOptions<PohodaSqlOptions> options,
        TimeProvider timeProvider)
        : base(scopeFactory, logger, CreateDelayProvider(options.Value))
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    protected override async Task ExecuteInScopeAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var exportService = serviceProvider.GetRequiredService<IPohodaExportService>();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var batchSize = Math.Max(1, _options.ExportWorkerBatchSize);

        var jobs = await dbContext.PohodaExportJobs
            .Where(job => job.Status == Models.PohodaExportJobStatus.Pending)
            .Where(job => job.NextAttemptAtUtc == null || job.NextAttemptAtUtc <= now)
            .OrderBy(job => job.CreatedAtUtc)
            .ThenBy(job => job.Id)
            .Take(batchSize)
            .Include(job => job.Order)
                .ThenInclude(order => order!.Items)
                    .ThenInclude(item => item.Course)
            .ToListAsync(stoppingToken)
            .ConfigureAwait(false);

        if (jobs.Count == 0)
        {
            return;
        }

        foreach (var job in jobs)
        {
            try
            {
                await exportService.ExportOrderAsync(job, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while exporting job {JobId}.", job.Id);
            }
        }
    }

    protected override string FailureMessage => "An error occurred while exporting orders to Pohoda.";

    private static Func<DateTime, CancellationToken, ValueTask<TimeSpan>> CreateDelayProvider(PohodaSqlOptions options)
    {
        var interval = options.ExportWorkerInterval <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(30)
            : options.ExportWorkerInterval;

        return (_, _) => new ValueTask<TimeSpan>(interval);
    }
}
