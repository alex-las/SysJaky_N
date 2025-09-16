using System;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Pages.Admin.CourseTerms;

public class CourseTermInputModel
{
    [Required]
    [Display(Name = "Course")]
    public int CourseId { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Start time")]
    public DateTime StartUtc { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "End time")]
    public DateTime EndUtc { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be at least 1.")]
    public int Capacity { get; set; } = 1;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Instructor")]
    public int? InstructorId { get; set; }
}
