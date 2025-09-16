using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class Instructor
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(200)]
    public string? Email { get; set; }

    [Phone]
    [Display(Name = "Phone number")]
    [StringLength(50)]
    public string? PhoneNumber { get; set; }

    [StringLength(4000)]
    [DataType(DataType.MultilineText)]
    public string? Bio { get; set; }

    public ICollection<CourseTerm> CourseTerms { get; set; } = new List<CourseTerm>();
}
