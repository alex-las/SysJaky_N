namespace SysJaky_N.Models;

public class PohodaExportJob
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order? Order { get; set; }

    public PohodaExportJobStatus Status { get; set; } = PohodaExportJobStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastAttemptAtUtc { get; set; }

    public DateTime? NextAttemptAtUtc { get; set; }

    public DateTime? SucceededAtUtc { get; set; }

    public DateTime? FailedAtUtc { get; set; }

    public string? LastError { get; set; }

    public string? DocumentNumber { get; set; }

    public string? DocumentId { get; set; }

    public string? Warnings { get; set; }
}

public enum PohodaExportJobStatus
{
    Pending,
    InProgress,
    Succeeded,
    Failed
}
