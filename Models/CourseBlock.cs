namespace SysJaky_N.Models;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public class CourseBlock
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Models.CourseBlock.Title.DisplayName")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Models.CourseBlock.Description.DisplayName")]
    public string? Description { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Validation.NonNegativeNumber")]
    [Display(Name = "Models.CourseBlock.Price.DisplayName")]
    public decimal Price { get; set; }

    public ICollection<Course> Modules { get; set; } = new List<Course>();
}

