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
    [Display(Name = "Valid From (UTC)")]
    public DateTime FromUtc { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Valid To (UTC)")]
    public DateTime ToUtc { get; set; }

    [Range(0, double.MaxValue)]
    [DataType(DataType.Currency)]
    [Display(Name = "New Price (excl. VAT)")]
    public decimal NewPriceExcl { get; set; }
}
