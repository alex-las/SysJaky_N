namespace SysJaky_N.Models;

using System.ComponentModel.DataAnnotations;

public class Course
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [DataType(DataType.Date)]
    public DateTime Date { get; set; }

    public CourseLevel Level { get; set; } = CourseLevel.Beginner;

    public CourseMode Mode { get; set; } = CourseMode.SelfPaced;

    [Range(0, int.MaxValue)]
    public int Duration { get; set; }

    [Range(0, int.MaxValue)]
    public int ReminderDays { get; set; }

    [StringLength(1000)]
    public string? ReminderMessage { get; set; }

    public CourseType Type { get; set; } = CourseType.Online;

    public bool IsActive { get; set; } = true;

    public int? CourseGroupId { get; set; }

    public virtual CourseGroup? CourseGroup { get; set; }

    public int? CourseBlockId { get; set; }

    public virtual CourseBlock? CourseBlock { get; set; }

    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public ICollection<CourseTag> CourseTags { get; set; } = new List<CourseTag>();
}

public enum CourseType
{
    Online,
    InPerson,
    Hybrid
}

public enum CourseLevel
{
    Beginner,
    Intermediate,
    Advanced
}

public enum CourseMode
{
    SelfPaced,
    InstructorLed,
    Blended
}
