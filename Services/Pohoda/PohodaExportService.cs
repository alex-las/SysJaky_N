using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaExportService : IPohodaExportService
{
    private readonly PohodaSqlClient _sqlClient;
    private readonly ILogger<PohodaExportService> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IPohodaSqlOptions _options;

    public PohodaExportService(
        PohodaSqlClient sqlClient,
        ApplicationDbContext dbContext,
        TimeProvider timeProvider,
        IPohodaSqlOptions options,
        ILogger<PohodaExportService> logger)
    {
        _sqlClient = sqlClient ?? throw new ArgumentNullException(nameof(sqlClient));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task QueueOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var existingJob = await _dbContext.PohodaExportJobs
            .SingleOrDefaultAsync(job => job.OrderId == order.Id, cancellationToken)
            .ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (existingJob is null)
        {
            var job = new PohodaExportJob
            {
                OrderId = order.Id,
                Status = PohodaExportJobStatus.Pending,
                CreatedAtUtc = now,
                NextAttemptAtUtc = now
            };

            await _dbContext.PohodaExportJobs.AddAsync(job, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existingJob.Status = PohodaExportJobStatus.Pending;
            existingJob.NextAttemptAtUtc = now;
            existingJob.FailedAtUtc = null;
            existingJob.SucceededAtUtc = null;
            existingJob.LastError = null;
            existingJob.AttemptCount = 0;
            existingJob.LastAttemptAtUtc = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportOrderAsync(PohodaExportJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var order = job.Order;
        if (order is null)
        {
            order = await _dbContext.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Course)
                .SingleOrDefaultAsync(o => o.Id == job.OrderId, cancellationToken)
                .ConfigureAwait(false);

            job.Order = order;
        }

        if (order is null)
        {
            MarkJobAsFailed(job, $"Order {job.OrderId} not found.");
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (order.Items.Count == 0)
        {
            await _dbContext.Entry(order)
                .Collection(o => o.Items)
                .Query()
                .Include(i => i.Course)
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var attemptStartedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        job.Status = PohodaExportJobStatus.InProgress;
        job.AttemptCount += 1;
        job.LastAttemptAtUtc = attemptStartedAtUtc;
        job.NextAttemptAtUtc = null;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var payload = PohodaOrderPayload.FromOrder(order);
        try
        {
            await _sqlClient.InsertOrderAsync(payload, cancellationToken).ConfigureAwait(false);

            job.Status = PohodaExportJobStatus.Succeeded;
            job.SucceededAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            job.LastError = null;
            job.NextAttemptAtUtc = null;

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Exported order {OrderId} to Pohoda (job {JobId}).", order.Id, job.Id);
        }
        catch (Exception ex)
        {
            HandleJobFailure(job, attemptStartedAtUtc, _timeProvider.GetUtcNow().UtcDateTime, ex);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogWarning(ex,
                "Failed to export order {OrderId} to Pohoda on attempt {Attempt}.",
                order.Id,
                job.AttemptCount);
        }
    }

    public Task MarkInvoiceGeneratedAsync(Order order, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            throw new ArgumentException("Invoice number must be provided.", nameof(invoiceNumber));
        }

        return _sqlClient.MarkInvoiceGeneratedAsync(order.Id, invoiceNumber, cancellationToken);
    }

    private void HandleJobFailure(
        PohodaExportJob job,
        DateTime attemptStartedAtUtc,
        DateTime attemptCompletedAtUtc,
        Exception exception)
    {
        var maxAttempts = Math.Max(1, _options.MaxRetryAttempts);
        var baseDelay = _options.RetryBaseDelay <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(30)
            : _options.RetryBaseDelay;
        var maxDelay = _options.RetryMaxDelay <= TimeSpan.Zero
            ? TimeSpan.FromMinutes(30)
            : _options.RetryMaxDelay;

        var backoffFactor = Math.Pow(2, Math.Max(0, job.AttemptCount - 1));
        var delay = TimeSpan.FromTicks((long)Math.Min(maxDelay.Ticks, baseDelay.Ticks * backoffFactor));

        job.LastError = exception.Message;
        job.NextAttemptAtUtc = attemptCompletedAtUtc + delay;

        if (job.AttemptCount >= maxAttempts)
        {
            job.Status = PohodaExportJobStatus.Failed;
            job.FailedAtUtc = attemptCompletedAtUtc;
        }
        else
        {
            job.Status = PohodaExportJobStatus.Pending;
        }
    }

    private void MarkJobAsFailed(PohodaExportJob job, string reason)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        job.Status = PohodaExportJobStatus.Failed;
        job.FailedAtUtc = now;
        job.LastError = reason;
        job.NextAttemptAtUtc = null;
    }
}
