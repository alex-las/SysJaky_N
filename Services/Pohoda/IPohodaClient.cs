using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SysJaky_N.Services.Pohoda;

public interface IPohodaClient
{
    Task<PohodaResponse> SendInvoiceAsync(string dataPack, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<InvoiceStatus>> ListInvoicesAsync(PohodaListFilter filter, CancellationToken cancellationToken = default);

    Task<bool> CheckStatusAsync(CancellationToken cancellationToken = default);
}
