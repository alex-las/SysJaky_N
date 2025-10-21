using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaIdempotencyStore : IPohodaIdempotencyStore
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PohodaIdempotencyStore> _logger;

    public PohodaIdempotencyStore(
        ApplicationDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<PohodaIdempotencyStore> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<PohodaIdempotencyRecord?> GetAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PohodaIdempotencyRecords
            .SingleOrDefaultAsync(record => record.OrderId == orderId, cancellationToken);
    }

    public async Task<PohodaIdempotencyRecord> UpsertAsync(
        int orderId,
        string dataPackId,
        PohodaIdempotencyStatus status,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataPackId);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var record = await _dbContext.PohodaIdempotencyRecords
            .SingleOrDefaultAsync(r => r.OrderId == orderId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            record = new PohodaIdempotencyRecord
            {
                OrderId = orderId,
                DataPackId = dataPackId,
                Status = status,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await _dbContext.PohodaIdempotencyRecords.AddAsync(record, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            record.DataPackId = dataPackId;
            record.Status = status;
            record.UpdatedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return record;
    }

    public async Task UpdateStatusAsync(
        int orderId,
        PohodaIdempotencyStatus status,
        CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.PohodaIdempotencyRecords
            .SingleOrDefaultAsync(r => r.OrderId == orderId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            _logger.LogWarning("Attempted to update Pohoda idempotency status for missing order {OrderId}.", orderId);
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        record.Status = status;
        record.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
