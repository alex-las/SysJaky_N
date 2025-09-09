using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class DiscountCode
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Code { get; set; } = string.Empty;

    [Range(0, 100)]
    public decimal? Percentage { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Amount { get; set; }

    [DataType(DataType.DateTime)]
    public DateTime ExpiresAt { get; set; }
}
