using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class CourseTerm
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Course")]
    public int CourseId { get; set; }
    public Course? Course { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Start (UTC)")]
    public DateTime StartUtc { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "End (UTC)")]
    public DateTime EndUtc { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be at least 1.")]
    public int Capacity { get; set; }

    [Display(Name = "Seats taken")]
    [Range(0, int.MaxValue)]
    public int SeatsTaken { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public DateTime? ReviewRequestSentAtUtc { get; set; }

    [Display(Name = "Instructor")]
    public int? InstructorId { get; set; }
    public Instructor? Instructor { get; set; }


    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<WaitlistEntry> WaitlistEntries { get; set; } = new List<WaitlistEntry>();
}
