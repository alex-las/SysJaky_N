namespace SysJaky_N.Models;

public class WaitlistEntry
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int CourseTermId { get; set; }
    public CourseTerm? CourseTerm { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Guid? ReservationToken { get; set; }
    public DateTime? ReservationExpiresAtUtc { get; set; }
    public bool ReservationConsumed { get; set; }
}
