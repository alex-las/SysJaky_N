using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public enum VoucherType
{
    Percentage,
    FixedAmount
}

public class Voucher
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public VoucherType Type { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Value { get; set; }

    [DataType(DataType.DateTime)]
    public DateTime? ExpiresUtc { get; set; }

    public int? MaxRedemptions { get; set; }

    public int UsedCount { get; set; }

    public int? AppliesToCourseId { get; set; }

    public Course? AppliesToCourse { get; set; }
}
