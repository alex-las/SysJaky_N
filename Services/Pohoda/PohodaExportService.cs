using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SysJaky_N.Data;
using SysJaky_N.Logging;
using SysJaky_N.Models;
using SysJaky_N.Services;
using Serilog.Context;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaExportService : IPohodaExportService
{
    private readonly IPohodaClient _xmlClient;
    private readonly PohodaXmlBuilder _xmlBuilder;
    private readonly ILogger<PohodaExportService> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly PohodaXmlOptions _options;
    private readonly IAuditService _auditService;
    private readonly IPohodaIdempotencyStore _idempotencyStore;
    private readonly string _contentRootPath;

    private static readonly JsonSerializerOptions AuditSerializerOptions = new(JsonSerializerDefaults.Web);

    public PohodaExportService(
        IPohodaClient xmlClient,
        PohodaXmlBuilder xmlBuilder,
        ApplicationDbContext dbContext,
        TimeProvider timeProvider,
        IOptions<PohodaXmlOptions> options,
        IAuditService auditService,
        IHostEnvironment hostEnvironment,
        IPohodaIdempotencyStore idempotencyStore,
        ILogger<PohodaExportService> logger)
    {
        _xmlClient = xmlClient ?? throw new ArgumentNullException(nameof(xmlClient));
        _xmlBuilder = xmlBuilder ?? throw new ArgumentNullException(nameof(xmlBuilder));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        var environment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
        _contentRootPath = string.IsNullOrWhiteSpace(environment.ContentRootPath)
            ? AppContext.BaseDirectory
            : environment.ContentRootPath;
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
            existingJob.DocumentNumber = null;
            existingJob.DocumentId = null;
            existingJob.Warnings = null;
        }

        await _idempotencyStore.UpsertAsync(
            order.Id,
            CreateDataPackId(order),
            PohodaIdempotencyStatus.Pending,
            cancellationToken).ConfigureAwait(false);

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
            await _idempotencyStore.UpdateStatusAsync(job.OrderId, PohodaIdempotencyStatus.Failed, cancellationToken)
                .ConfigureAwait(false);
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

        await _idempotencyStore.UpdateStatusAsync(order.Id, PohodaIdempotencyStatus.InProgress, cancellationToken)
            .ConfigureAwait(false);

        var invoice = OrderToInvoiceMapper.Map(order);
        var payload = _xmlBuilder.BuildIssuedInvoiceXml(invoice, _options.Application);

        var correlationId = CreateCorrelationId(job);
        using var correlationScope = LogContext.PushProperty("CorrelationId", correlationId);

        if (!_options.Enabled)
        {
            await HandleDisabledIntegrationAsync(job, order, payload, correlationId, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (job.AttemptCount > 1)
        {
            var completedBeforeRetry = await TryCompleteFromExistingInvoiceAsync(order, job, correlationId, cancellationToken)
                .ConfigureAwait(false);

            if (completedBeforeRetry)
            {
                return;
            }
        }

        try
        {
            var response = await _xmlClient.SendInvoiceAsync(payload, correlationId, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(response.DocumentNumber))
            {
                order.InvoiceNumber = response.DocumentNumber;
            }

            job.DocumentNumber = response.DocumentNumber;
            job.DocumentId = response.DocumentId;
            job.Warnings = response.Warnings.Count > 0 ? string.Join("\n", response.Warnings) : null;

            job.Status = PohodaExportJobStatus.Succeeded;
            job.SucceededAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            job.LastError = null;
            job.NextAttemptAtUtc = null;

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _idempotencyStore.UpdateStatusAsync(order.Id, PohodaIdempotencyStatus.Succeeded, cancellationToken)
                .ConfigureAwait(false);

            using (BeginPohodaExportScope(job, order))
            {
                _logger.LogInformation(
                    "Exported order {OrderId} to Pohoda (job {JobId}) with document {DocumentNumber} (ID: {DocumentId}).",
                    order.Id,
                    job.Id,
                    job.DocumentNumber ?? order.InvoiceNumber,
                    job.DocumentId);
            }

            await LogAuditEventAsync(
                action: "PohodaExportSucceeded",
                job: job,
                order: order,
                result: "Succeeded",
                response: response,
                payloadLog: response.PayloadLog,
                correlationId: correlationId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (IsTimeoutException(ex, cancellationToken))
            {
                var completed = await TryCompleteFromExistingInvoiceAsync(order, job, correlationId, cancellationToken)
                    .ConfigureAwait(false);

                if (completed)
                {
                    return;
                }
            }

            var attemptCompletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            HandleJobFailure(job, attemptStartedAtUtc, attemptCompletedAtUtc, ex);

            var status = job.Status == PohodaExportJobStatus.Failed
                ? PohodaIdempotencyStatus.Failed
                : PohodaIdempotencyStatus.Pending;

            await _idempotencyStore.UpdateStatusAsync(order.Id, status, cancellationToken).ConfigureAwait(false);
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
                error: ex.Message,
                payloadLog: (ex as PohodaXmlException)?.PayloadLog,
                correlationId: correlationId).ConfigureAwait(false);
        }
    }

    public async Task MarkInvoiceGeneratedAsync(Order order, string invoicePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        _ = invoicePath;
        if (string.IsNullOrWhiteSpace(order.InvoiceNumber))
        {
            return;
        }

        if (!_options.Enabled)
        {
            return;
        }

        try
        {
            var filter = CreateInvoiceFilter(order);
            await FetchInvoiceStatusesAsync(filter, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh invoice {InvoiceNumber} for order {OrderId}.", order.InvoiceNumber, order.Id);
        }
    }

    private static string CreateDataPackId(Order order)
        => $"Invoice-{order.Id.ToString(CultureInfo.InvariantCulture)}";

    private static string CreateCorrelationId(PohodaExportJob job)
        => $"pohoda-export-{job.Id}-attempt-{Math.Max(1, job.AttemptCount)}";

    private static PohodaListFilter CreateInvoiceFilter(Order order)
    {
        var createdDate = DateOnly.FromDateTime(order.CreatedAt);
        return new PohodaListFilter
        {
            Number = order.InvoiceNumber,
            VariableSymbol = order.Id.ToString(CultureInfo.InvariantCulture),
            DateFrom = createdDate,
            DateTo = createdDate
        };
    }

    private Task<IReadOnlyCollection<InvoiceStatus>> FetchInvoiceStatusesAsync(
        PohodaListFilter filter,
        CancellationToken cancellationToken)
    {
        return _xmlClient.ListInvoicesAsync(filter, cancellationToken);
    }

    private async Task<bool> TryCompleteFromExistingInvoiceAsync(
        Order order,
        PohodaExportJob job,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var filter = CreateInvoiceFilter(order);
        IReadOnlyCollection<InvoiceStatus> invoices;

        try
        {
            invoices = await FetchInvoiceStatusesAsync(filter, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            using (BeginPohodaExportScope(job, order))
            {
                _logger.LogWarning(ex, "Failed to check existing invoice for order {OrderId}.", order.Id);
            }

            await LogAuditEventAsync(
                action: "PohodaInvoiceCheckFailed",
                job: job,
                order: order,
                result: "Failed",
                error: ex.Message,
                correlationId: correlationId).ConfigureAwait(false);

            return false;
        }

        var match = FindMatchingInvoice(invoices, order, job);

        using (BeginPohodaExportScope(job, order))
        {
            if (match is not null)
            {
                _logger.LogInformation(
                    "Existing invoice {InvoiceNumber} confirmed for order {OrderId} during retry.",
                    match.Number ?? order.InvoiceNumber ?? job.DocumentNumber ?? "(unknown)",
                    order.Id);
            }
            else
            {
                _logger.LogInformation(
                    "No existing invoice found for order {OrderId} during retry check.",
                    order.Id);
            }
        }

        await LogAuditEventAsync(
            action: "PohodaInvoiceCheck",
            job: job,
            order: order,
            result: match is not null ? "InvoiceFound" : "InvoiceNotFound",
            error: match is null ? "Invoice not found during timeout verification." : null,
            correlationId: correlationId).ConfigureAwait(false);

        if (match is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(match.Number))
        {
            order.InvoiceNumber = match.Number;
            job.DocumentNumber = match.Number;
        }

        job.Status = PohodaExportJobStatus.Succeeded;
        job.SucceededAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        job.LastError = null;
        job.NextAttemptAtUtc = null;
        job.FailedAtUtc = null;
        job.Warnings = null;

        await _idempotencyStore.UpdateStatusAsync(order.Id, PohodaIdempotencyStatus.Succeeded, cancellationToken)
            .ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await LogAuditEventAsync(
            action: "PohodaExportSucceeded",
            job: job,
            order: order,
            result: "SucceededExistingInvoice",
            correlationId: correlationId).ConfigureAwait(false);

        return true;
    }

    private static InvoiceStatus? FindMatchingInvoice(
        IReadOnlyCollection<InvoiceStatus> invoices,
        Order order,
        PohodaExportJob job)
    {
        if (invoices.Count == 0)
        {
            return null;
        }

        var variableSymbol = order.Id.ToString(CultureInfo.InvariantCulture);
        var knownNumbers = new[]
            {
                order.InvoiceNumber,
                job.DocumentNumber
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();

        return invoices.FirstOrDefault(status =>
            (!string.IsNullOrWhiteSpace(status.SymVar) && string.Equals(status.SymVar, variableSymbol, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(status.Number) && knownNumbers.Any(number => string.Equals(number, status.Number, StringComparison.OrdinalIgnoreCase))));
    }

    private static bool IsTimeoutException(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException operationCanceled)
        {
            if (cancellationToken.IsCancellationRequested && operationCanceled.CancellationToken == cancellationToken)
            {
                return false;
            }

            return exception is TaskCanceledException;
        }

        return exception is TaskCanceledException or TimeoutException;
    }

    private async Task HandleDisabledIntegrationAsync(
        PohodaExportJob job,
        Order order,
        string payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var filePath = await SaveExportToFileAsync(order, payload, cancellationToken).ConfigureAwait(false);

        job.Status = PohodaExportJobStatus.Succeeded;
        job.SucceededAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        job.LastError = null;
        job.NextAttemptAtUtc = null;
        job.FailedAtUtc = null;
        job.DocumentNumber = null;
        job.DocumentId = null;
        job.Warnings = null;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _idempotencyStore.UpdateStatusAsync(order.Id, PohodaIdempotencyStatus.Succeeded, cancellationToken)
            .ConfigureAwait(false);

        using (BeginPohodaExportScope(job, order))
        {
            _logger.LogInformation(
                "Pohoda integration disabled. Export for order {OrderId} stored at {FilePath} (job {JobId}).",
                order.Id,
                filePath,
                job.Id);
        }

        await LogAuditEventAsync(
            action: "PohodaExportSavedToFile",
            job: job,
            order: order,
            result: "SavedToFile",
            correlationId: correlationId).ConfigureAwait(false);
    }

    private async Task<string> SaveExportToFileAsync(Order order, string payload, CancellationToken cancellationToken)
    {
        var directory = ResolveExportDirectory();

        string targetDirectory;
        try
        {
            targetDirectory = Path.GetFullPath(directory);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to resolve Pohoda export directory '{directory}'.", ex);
        }

        Directory.CreateDirectory(targetDirectory);

        var timestamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var fileName = $"pohoda-order-{order.Id}-{timestamp}.xml";
        var filePath = Path.Combine(targetDirectory, fileName);

        var encoding = CreateEncoding(_options.EncodingName);
        var data = encoding.GetBytes(payload);
        await File.WriteAllBytesAsync(filePath, data, cancellationToken).ConfigureAwait(false);

        return filePath;
    }

    private string ResolveExportDirectory()
    {
        var directory = string.IsNullOrWhiteSpace(_options.ExportDirectory)
            ? "temp"
            : _options.ExportDirectory.Trim();

        if (string.IsNullOrEmpty(directory))
        {
            directory = "temp";
        }

        if (Path.IsPathRooted(directory))
        {
            return Path.GetFullPath(directory);
        }

        return Path.GetFullPath(Path.Combine(_contentRootPath, directory));
    }

    private static Encoding CreateEncoding(string encodingName)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var fallbackEncoding = "windows-1250";
        var name = string.IsNullOrWhiteSpace(encodingName) ? fallbackEncoding : encodingName;

        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            return Encoding.GetEncoding(fallbackEncoding);
        }
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

    private async Task LogAuditEventAsync(
        string action,
        PohodaExportJob job,
        Order? order,
        string result,
        string? error = null,
        PohodaResponse? response = null,
        PohodaPayloadLog? payloadLog = null,
        string? correlationId = null)
    {
        payloadLog ??= response?.PayloadLog;

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
            job.DocumentNumber,
            job.DocumentId,
            job.Warnings,
            Result = result,
            Error = error,
            CorrelationId = correlationId,
            PayloadLog = payloadLog is null
                ? null
                : new
                {
                    payloadLog.RequestPath,
                    payloadLog.ResponsePath
                },
            Response = response is null
                ? null
                : new
                {
                    response.State,
                    response.DocumentNumber,
                    response.DocumentId,
                    response.Warnings,
                    response.Errors
                },
            Order = order is null
                ? null
                : new
                {
                    order.Id,
                    Status = order.Status.ToString(),
                    order.Total,
                    order.UserId,
                    order.InvoiceNumber
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
