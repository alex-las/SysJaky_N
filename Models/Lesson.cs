using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class Lesson
{
    public int Id { get; set; }

    [Required]
    public int CourseId { get; set; }

    [Required]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty;

    [Required]
    [StringLength(2048)]
    [DataType(DataType.Url)]
    public string ContentUrl { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Order { get; set; }

    public Course Course { get; set; } = null!;

    public ICollection<LessonProgress> ProgressRecords { get; set; } = new List<LessonProgress>();
}
