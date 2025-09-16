using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class PaymentId
{
    [Key]
    [StringLength(255)]
    public string Id { get; set; } = string.Empty;

    [DataType(DataType.DateTime)]
    public DateTime ProcessedUtc { get; set; } = DateTime.UtcNow;
}
