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
    [Display(Name = "Code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Voucher type")]
    public VoucherType Type { get; set; }

    [Range(0, double.MaxValue)]
    [Display(Name = "Value")]
    public decimal Value { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Expires (UTC)")]
    public DateTime? ExpiresUtc { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Maximum redemptions must be greater than zero.")]
    [Display(Name = "Max redemptions")]
    public int? MaxRedemptions { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Used count")]
    public int UsedCount { get; set; }

    public int? AppliesToCourseId { get; set; }

    [Display(Name = "Applies to course")]
    public Course? AppliesToCourse { get; set; }
}
