using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class PriceSchedule
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Models.PriceSchedule.CourseId.DisplayName")]
    public int CourseId { get; set; }

    public Course? Course { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Models.PriceSchedule.FromUtc.DisplayName")]
    public DateTime FromUtc { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Models.PriceSchedule.ToUtc.DisplayName")]
    public DateTime ToUtc { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Validation.NewPricePositive")]
    [DataType(DataType.Currency)]
    [Display(Name = "Models.PriceSchedule.NewPriceExcl.DisplayName")]
    public decimal NewPriceExcl { get; set; }
}
