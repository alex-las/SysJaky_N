namespace SysJaky_N.Models;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public class CourseCategory
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Course> Courses { get; set; } = new List<Course>();

    public ICollection<CourseCategoryTranslation> Translations { get; set; } = new List<CourseCategoryTranslation>();
}
