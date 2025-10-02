using System;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Pages.Admin.CourseTerms;

public class CourseTermInputModel
{
    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Pages.Admin.CourseTerms.Input.CourseId.DisplayName")]
    public int CourseId { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Pages.Admin.CourseTerms.Input.StartUtc.DisplayName")]
    public DateTime StartUtc { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Pages.Admin.CourseTerms.Input.EndUtc.DisplayName")]
    public DateTime EndUtc { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Validation.PositiveInteger")]
    public int Capacity { get; set; } = 1;

    [Display(Name = "Pages.Admin.CourseTerms.Input.IsActive.DisplayName")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Pages.Admin.CourseTerms.Input.InstructorId.DisplayName")]
    public int? InstructorId { get; set; }
}
