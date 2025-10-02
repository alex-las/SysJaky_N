using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class CourseTerm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Models.CourseTerm.CourseId.DisplayName")]
    public int CourseId { get; set; }
    public Course? Course { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Models.CourseTerm.StartUtc.DisplayName")]
    public DateTime StartUtc { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Models.CourseTerm.EndUtc.DisplayName")]
    public DateTime EndUtc { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Validation.PositiveInteger")]
    public int Capacity { get; set; }

    [Display(Name = "Models.CourseTerm.SeatsTaken.DisplayName")]
    [Range(0, int.MaxValue, ErrorMessage = "Validation.NonNegativeNumber")]
    public int SeatsTaken { get; set; }

    [Display(Name = "Models.CourseTerm.IsActive.DisplayName")]
    public bool IsActive { get; set; } = true;

    public DateTime? ReviewRequestSentAtUtc { get; set; }

    [Display(Name = "Models.CourseTerm.InstructorId.DisplayName")]
    public int? InstructorId { get; set; }
    public Instructor? Instructor { get; set; }


    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<WaitlistEntry> WaitlistEntries { get; set; } = new List<WaitlistEntry>();
}
