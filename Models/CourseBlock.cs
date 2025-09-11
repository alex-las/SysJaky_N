namespace SysJaky_N.Models;

using System.ComponentModel.DataAnnotations;

public class CourseBlock
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public ICollection<Course> Modules { get; set; } = new List<Course>();
}

