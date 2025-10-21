using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SysJaky_N.Models;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaExportService : IPohodaExportService
{
    private readonly PohodaSqlClient _sqlClient;
    private readonly ILogger<PohodaExportService> _logger;

    public PohodaExportService(PohodaSqlClient sqlClient, ILogger<PohodaExportService> logger)
    {
        _sqlClient = sqlClient ?? throw new ArgumentNullException(nameof(sqlClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task QueueOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        // For now we export immediately; this hook allows future background queue implementation.
        return ExportOrderAsync(order, cancellationToken);
    }

    public async Task ExportOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var payload = PohodaOrderPayload.FromOrder(order);
        await _sqlClient.InsertOrderAsync(payload, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Exported order {OrderId} to Pohoda", order.Id);
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
}
