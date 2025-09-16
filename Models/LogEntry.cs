using System;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class LogEntry
{
    public long Id { get; set; }

    public DateTime Timestamp { get; set; }

    [MaxLength(32)]
    public string Level { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Exception { get; set; }

    public string? Properties { get; set; }

    [MaxLength(128)]
    public string? SourceContext { get; set; }

    [MaxLength(100)]
    public string? CorrelationId { get; set; }
}

