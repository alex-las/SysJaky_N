namespace SysJaky_N.Models;

using System.ComponentModel.DataAnnotations;

public class CourseCategoryTranslation
{
    public int Id { get; set; }

    [Required]
    public int CategoryId { get; set; }

    [Required]
    [StringLength(10)]
    public string Locale { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public CourseCategory Category { get; set; } = default!;
}
