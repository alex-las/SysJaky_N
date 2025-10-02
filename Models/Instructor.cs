using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class Instructor
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(200, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Models.Instructor.FullName.DisplayName")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Validation.EmailAddress")]
    [StringLength(200, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Models.Instructor.Email.DisplayName")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Validation.Phone")]
    [Display(Name = "Models.Instructor.PhoneNumber.DisplayName")]
    [StringLength(50, ErrorMessage = "Validation.StringLength")]
    public string? PhoneNumber { get; set; }

    [StringLength(4000, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Models.Instructor.Bio.DisplayName")]
    [DataType(DataType.MultilineText)]
    public string? Bio { get; set; }

    public ICollection<CourseTerm> CourseTerms { get; set; } = new List<CourseTerm>();
}
