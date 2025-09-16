using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class PriceSchedule
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Course")]
    public int CourseId { get; set; }

    public Course? Course { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Valid From")]
    public DateTime FromUtc { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Valid To")]
    public DateTime ToUtc { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "The new price must be greater than zero.")]
    [DataType(DataType.Currency)]
    [Display(Name = "New Price (excl. VAT)")]
    public decimal NewPriceExcl { get; set; }
}
