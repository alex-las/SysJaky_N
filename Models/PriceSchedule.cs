using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class PriceSchedule
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Kurz je povinný.")]
    [Display(Name = "Kurz")]
    public int CourseId { get; set; }

    public Course? Course { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Platí od")]
    public DateTime FromUtc { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Platí do")]
    public DateTime ToUtc { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Nová cena musí být větší než nula.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Nová cena (bez DPH)")]
    public decimal NewPriceExcl { get; set; }
}
