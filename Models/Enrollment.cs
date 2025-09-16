namespace SysJaky_N.Models;

public class Enrollment
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int CourseTermId { get; set; }
    public CourseTerm? CourseTerm { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Pending;

    public DateTime? CheckedInAtUtc { get; set; }

    public Certificate? Certificate { get; set; }
}

public enum EnrollmentStatus
{
    Pending,
    Confirmed,
    Cancelled
}
