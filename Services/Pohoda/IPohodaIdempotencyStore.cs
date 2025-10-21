using SysJaky_N.Models;

namespace SysJaky_N.Services.Pohoda;

public interface IPohodaIdempotencyStore
{
    Task<PohodaIdempotencyRecord?> GetAsync(int orderId, CancellationToken cancellationToken = default);

    Task<PohodaIdempotencyRecord> UpsertAsync(
        int orderId,
        string dataPackId,
        PohodaIdempotencyStatus status,
        CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(
        int orderId,
        PohodaIdempotencyStatus status,
        CancellationToken cancellationToken = default);
}
