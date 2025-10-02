using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class Testimonial
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(120, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Models.Testimonial.FullName.DisplayName")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(80, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Models.Testimonial.Position.DisplayName")]
    public string Position { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(120, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Models.Testimonial.Company.DisplayName")]
    public string Company { get; set; } = string.Empty;

    [StringLength(256, ErrorMessage = "Validation.StringLength")]
    [Url(ErrorMessage = "Validation.Url")]
    [Display(Name = "Models.Testimonial.PhotoUrl.DisplayName")]
    public string? PhotoUrl { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(150, ErrorMessage = "Validation.StringLength")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Models.Testimonial.Quote.DisplayName")]
    public string Quote { get; set; } = string.Empty;

    [Range(1, 5, ErrorMessage = "Validation.Range")]
    [Display(Name = "Models.Testimonial.Rating.DisplayName")]
    public int Rating { get; set; } = 5;

    [Display(Name = "Models.Testimonial.IsPublished.DisplayName")]
    public bool IsPublished { get; set; }

    [Display(Name = "Models.Testimonial.ConsentGranted.DisplayName")]
    public bool ConsentGranted { get; set; }

    [DataType(DataType.DateTime)]
    public DateTime? ConsentGrantedAtUtc { get; set; }

    [StringLength(180, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Models.Testimonial.PhotoAltText.DisplayName")]
    public string? PhotoAltText { get; set; }
}
