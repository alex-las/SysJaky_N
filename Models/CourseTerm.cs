namespace SysJaky_N.Models;

public class CourseTerm
{
    public int Id { get; set; }

    public int CourseId { get; set; }

    public Course? Course { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public int Capacity { get; set; }

    public int SeatsTaken { get; set; }

    public bool IsActive { get; set; } = true;
}
