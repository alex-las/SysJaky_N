namespace SysJaky_N.Models;

using System.ComponentModel.DataAnnotations;

public class CourseGroup
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public ICollection<Course>? Courses { get; set; }
}
