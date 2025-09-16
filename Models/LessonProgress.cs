using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class LessonProgress
{
    [Required]
    public int LessonId { get; set; }

    public Lesson Lesson { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    [Range(0, 100)]
    public int Progress { get; set; }

    public DateTime LastSeenUtc { get; set; }
}
