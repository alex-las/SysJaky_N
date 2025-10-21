using System.Threading;
using System.Threading.Tasks;
using SysJaky_N.Models;

namespace SysJaky_N.Services.Pohoda;

public interface IPohodaExportService
{
    Task QueueOrderAsync(Order order, CancellationToken cancellationToken = default);

    Task ExportOrderAsync(Order order, CancellationToken cancellationToken = default);

    Task MarkInvoiceGeneratedAsync(Order order, string invoiceNumber, CancellationToken cancellationToken = default);
}
