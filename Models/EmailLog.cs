using System;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class EmailLog
{
    public int Id { get; set; }

    [MaxLength(320)]
    public string To { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Template { get; set; } = string.Empty;

    public string? PayloadJson { get; set; }

    public DateTime SentUtc { get; set; }

    [MaxLength(256)]
    public string Status { get; set; } = string.Empty;
}
