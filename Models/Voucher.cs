using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public enum VoucherType
{
    [Display(Name = "Models.VoucherType.Percentage.DisplayName")]
    Percentage,

    [Display(Name = "Models.VoucherType.FixedAmount.DisplayName")]
    FixedAmount
}

public class Voucher
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Models.Voucher.Code.DisplayName")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Models.Voucher.Type.DisplayName")]
    public VoucherType Type { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Validation.NonNegativeNumber")]
    [Display(Name = "Models.Voucher.Value.DisplayName")]
    public decimal Value { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Models.Voucher.ExpiresUtc.DisplayName")]
    public DateTime? ExpiresUtc { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Validation.PositiveInteger")]
    [Display(Name = "Models.Voucher.MaxRedemptions.DisplayName")]
    public int? MaxRedemptions { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Validation.NonNegativeNumber")]
    [Display(Name = "Models.Voucher.UsedCount.DisplayName")]
    public int UsedCount { get; set; }

    public int? AppliesToCourseId { get; set; }

    [Display(Name = "Models.Voucher.AppliesToCourse.DisplayName")]
    public Course? AppliesToCourse { get; set; }
}
