using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using SysJaky_N.Logging;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaExportService : IPohodaExportService
{
    private readonly PohodaSqlClient _sqlClient;
    private readonly ILogger<PohodaExportService> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IPohodaSqlOptions _options;
    private readonly IAuditService _auditService;

    private static readonly JsonSerializerOptions AuditSerializerOptions = new(JsonSerializerDefaults.Web);

    public PohodaExportService(
        PohodaSqlClient sqlClient,
        ApplicationDbContext dbContext,
        TimeProvider timeProvider,
        IPohodaSqlOptions options,
        IAuditService auditService,
        ILogger<PohodaExportService> logger)
    {
        _sqlClient = sqlClient ?? throw new ArgumentNullException(nameof(sqlClient));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task QueueOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var existingJob = await _dbContext.PohodaExportJobs
            .SingleOrDefaultAsync(job => job.OrderId == order.Id, cancellationToken)
            .ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        PohodaExportJob jobRecord;

        if (existingJob is null)
        {
            jobRecord = new PohodaExportJob
            {
                OrderId = order.Id,
                Status = PohodaExportJobStatus.Pending,
                CreatedAtUtc = now,
                NextAttemptAtUtc = now
            };

            await _dbContext.PohodaExportJobs.AddAsync(jobRecord, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            jobRecord = existingJob;
            existingJob.Status = PohodaExportJobStatus.Pending;
            existingJob.NextAttemptAtUtc = now;
            existingJob.FailedAtUtc = null;
            existingJob.SucceededAtUtc = null;
            existingJob.LastError = null;
            existingJob.AttemptCount = 0;
            existingJob.LastAttemptAtUtc = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await LogAuditEventAsync(
            action: "PohodaExportQueued",
            job: jobRecord,
            order: order,
            result: "Queued").ConfigureAwait(false);
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
            var reason = $"Order {job.OrderId} not found.";
            MarkJobAsFailed(job, reason);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            using (BeginPohodaExportScope(job, null))
            {
                _logger.LogWarning("Attempted to export missing order {OrderId} (job {JobId}).", job.OrderId, job.Id);
            }

            await LogAuditEventAsync(
                action: "PohodaExportFailed",
                job: job,
                order: null,
                result: "Failed",
                error: reason).ConfigureAwait(false);
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

            using (BeginPohodaExportScope(job, order))
            {
                _logger.LogInformation("Exported order {OrderId} to Pohoda (job {JobId}).", order.Id, job.Id);
            }

            await LogAuditEventAsync(
                action: "PohodaExportSucceeded",
                job: job,
                order: order,
                result: "Succeeded").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            HandleJobFailure(job, attemptStartedAtUtc, _timeProvider.GetUtcNow().UtcDateTime, ex);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            using (BeginPohodaExportScope(job, order))
            {
                _logger.LogWarning(ex,
                    "Failed to export order {OrderId} to Pohoda on attempt {Attempt}.",
                    order.Id,
                    job.AttemptCount);
            }

            await LogAuditEventAsync(
                action: "PohodaExportFailed",
                job: job,
                order: order,
                result: "Failed",
                error: ex.Message).ConfigureAwait(false);
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

    private IDisposable BeginPohodaExportScope(PohodaExportJob job, Order? order)
    {
        return _logger.BeginScope(new Dictionary<string, object?>
        {
            ["EventName"] = LogEventNames.PohodaExportJob,
            ["PohodaJobId"] = job.Id,
            ["OrderId"] = order?.Id ?? job.OrderId,
            ["PohodaJobStatus"] = job.Status.ToString()
        }) ?? NullDisposable.Instance;
    }

    private async Task LogAuditEventAsync(string action, PohodaExportJob job, Order? order, string result, string? error = null)
    {
        var auditPayload = new
        {
            JobId = job.Id,
            JobStatus = job.Status.ToString(),
            job.AttemptCount,
            job.CreatedAtUtc,
            job.LastAttemptAtUtc,
            job.NextAttemptAtUtc,
            job.SucceededAtUtc,
            job.FailedAtUtc,
            job.LastError,
            Result = result,
            Error = error,
            Order = order is null
                ? null
                : new
                {
                    order.Id,
                    Status = order.Status.ToString(),
                    order.Total,
                    order.UserId
                }
        };

        try
        {
            var serialized = JsonSerializer.Serialize(auditPayload, AuditSerializerOptions);
            await _auditService.LogAsync(null, action, serialized).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            using (BeginPohodaExportScope(job, order))
            {
                _logger.LogError(ex, "Failed to write audit log for Pohoda export job {JobId}.", job.Id);
            }
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        private NullDisposable()
        {
        }

        public void Dispose()
        {
        }
    }
}
