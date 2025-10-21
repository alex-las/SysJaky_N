using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;

namespace SysJaky_N.Services.Pohoda;

public class PohodaSqlClient
{
    private const string InsertDokladSql = @"
INSERT INTO Doklady (OrderId, ExternalOrderNumber, CustomerId, CreatedAt, PriceExclVat, Vat, TotalInclVat, Discount, PaymentReference, InvoiceNumber)
VALUES (@OrderId, @ExternalOrderNumber, @CustomerId, @CreatedAt, @PriceExclVat, @Vat, @TotalInclVat, @Discount, @PaymentReference, @InvoiceNumber);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

    private const string InsertPolozkaSql = @"
INSERT INTO Polozky (DokladId, OrderItemId, OrderId, CourseId, Name, Quantity, UnitPriceExclVat, VatRate, TotalExclVat, VatAmount, TotalInclVat, Discount)
VALUES (@DokladId, @OrderItemId, @OrderId, @CourseId, @Name, @Quantity, @UnitPriceExclVat, @VatRate, @TotalExclVat, @VatAmount, @TotalInclVat, @Discount);";

    private const string MarkInvoiceSql = @"
UPDATE Doklady
SET InvoiceNumber = @InvoiceNumber,
    InvoiceGeneratedAt = @GeneratedAt
WHERE OrderId = @OrderId;";

    private readonly IPohodaSqlOptions _options;
    private readonly ILogger<PohodaSqlClient> _logger;

    public PohodaSqlClient(IPohodaSqlOptions options, ILogger<PohodaSqlClient> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public virtual async Task InsertOrderAsync(PohodaOrderPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var dokladParameters = new
            {
                payload.Doklad.OrderId,
                payload.Doklad.ExternalOrderNumber,
                payload.Doklad.CustomerId,
                payload.Doklad.CreatedAt,
                payload.Doklad.PriceExclVat,
                payload.Doklad.Vat,
                payload.Doklad.TotalInclVat,
                payload.Doklad.Discount,
                payload.Doklad.PaymentReference,
                payload.Doklad.InvoiceNumber
            };

            var dokladCommand = new CommandDefinition(
                InsertDokladSql,
                dokladParameters,
                transaction,
                cancellationToken: cancellationToken);

            var dokladId = await connection.ExecuteScalarAsync<int>(dokladCommand).ConfigureAwait(false);

            foreach (var item in payload.Polozky)
            {
                item.DokladId = dokladId;
                var itemParameters = new
                {
                    DokladId = dokladId,
                    item.OrderItemId,
                    item.OrderId,
                    item.CourseId,
                    item.Name,
                    item.Quantity,
                    item.UnitPriceExclVat,
                    item.VatRate,
                    item.TotalExclVat,
                    item.VatAmount,
                    item.TotalInclVat,
                    item.Discount
                };

                var itemCommand = new CommandDefinition(
                    InsertPolozkaSql,
                    itemParameters,
                    transaction,
                    cancellationToken: cancellationToken);

                await connection.ExecuteAsync(itemCommand).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "Failed to export order {OrderId} to Pohoda.", payload.Doklad.OrderId);
            throw;
        }
    }

    public virtual async Task MarkInvoiceGeneratedAsync(int orderId, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            throw new ArgumentException("Invoice number must be provided.", nameof(invoiceNumber));
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var parameters = new
        {
            OrderId = orderId,
            InvoiceNumber = invoiceNumber,
            GeneratedAt = DateTime.UtcNow
        };

        var command = new CommandDefinition(MarkInvoiceSql, parameters, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    protected virtual SqlConnection CreateConnection()
    {
        var connectionString = BuildConnectionString();
        return new SqlConnection(connectionString);
    }

    private string BuildConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            return _options.ConnectionString;
        }

        if (string.IsNullOrWhiteSpace(_options.Server) || string.IsNullOrWhiteSpace(_options.Database))
        {
            throw new InvalidOperationException("Pohoda SQL options are not configured.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = _options.Server,
            InitialCatalog = _options.Database,
            MultipleActiveResultSets = true
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            builder.UserID = _options.Username;
            builder.Password = _options.Password ?? string.Empty;
            builder.IntegratedSecurity = false;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        return builder.ConnectionString;
    }
}
