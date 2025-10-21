using System;

namespace SysJaky_N.Models;

public sealed class PohodaIdempotencyRecord
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public string DataPackId { get; set; } = string.Empty;

    public PohodaIdempotencyStatus Status { get; set; } = PohodaIdempotencyStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum PohodaIdempotencyStatus
{
    Pending,
    InProgress,
    Succeeded,
    Failed
}
