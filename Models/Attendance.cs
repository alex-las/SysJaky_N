namespace SysJaky_N.Models;

public class Attendance
{
    public int Id { get; set; }

    public int EnrollmentId { get; set; }
    public Enrollment? Enrollment { get; set; }

    public DateTime CheckedInAtUtc { get; set; }
}
