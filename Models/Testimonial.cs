using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class Testimonial
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    [Display(Name = "Jméno a příjmení")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    [Display(Name = "Pozice")]
    public string Position { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    [Display(Name = "Společnost")]
    public string Company { get; set; } = string.Empty;

    [StringLength(256)]
    [Url]
    [Display(Name = "URL fotografie")]
    public string? PhotoUrl { get; set; }

    [Required]
    [StringLength(150)]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Citace")]
    public string Quote { get; set; } = string.Empty;

    [Range(1, 5)]
    [Display(Name = "Hodnocení")]
    public int Rating { get; set; } = 5;

    [Display(Name = "Publikovat")]
    public bool IsPublished { get; set; }

    [Display(Name = "Souhlas se zpracováním osobních údajů")]
    public bool ConsentGranted { get; set; }

    [DataType(DataType.DateTime)]
    public DateTime? ConsentGrantedAtUtc { get; set; }

    [StringLength(180)]
    [Display(Name = "Alternativní text fotografie")]
    public string? PhotoAltText { get; set; }
}
