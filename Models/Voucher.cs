using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public enum VoucherType
{
    [Display(Name = "Procentuální sleva")]
    Percentage,

    [Display(Name = "Fixní částka")]
    FixedAmount
}

public class Voucher
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Kód")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Typ voucheru")]
    public VoucherType Type { get; set; }

    [Range(0, double.MaxValue)]
    [Display(Name = "Hodnota")]
    public decimal Value { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Platnost (UTC)")]
    public DateTime? ExpiresUtc { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Maximální počet uplatnění musí být větší než nula.")]
    [Display(Name = "Maximální počet uplatnění")]
    public int? MaxRedemptions { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Počet uplatnění")]
    public int UsedCount { get; set; }

    public int? AppliesToCourseId { get; set; }

    [Display(Name = "Platí pro kurz")]
    public Course? AppliesToCourse { get; set; }
}
