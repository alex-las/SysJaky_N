namespace SysJaky_N.Models;

using System.ComponentModel.DataAnnotations;

public class Tag
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public ICollection<CourseTag> CourseTags { get; set; } = new List<CourseTag>();
}
