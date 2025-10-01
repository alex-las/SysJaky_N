using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class Instructor
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Pole {0} je povinné.")]
    [StringLength(200, ErrorMessage = "Pole {0} může mít maximálně {1} znaků.")]
    [Display(Name = "Celé jméno")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Zadejte platnou e-mailovou adresu.")]
    [StringLength(200, ErrorMessage = "Pole {0} může mít maximálně {1} znaků.")]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Zadejte platné telefonní číslo.")]
    [Display(Name = "Telefon")]
    [StringLength(50, ErrorMessage = "Pole {0} může mít maximálně {1} znaků.")]
    public string? PhoneNumber { get; set; }

    [StringLength(4000, ErrorMessage = "Pole {0} může mít maximálně {1} znaků.")]
    [Display(Name = "Životopis")]
    [DataType(DataType.MultilineText)]
    public string? Bio { get; set; }

    public ICollection<CourseTerm> CourseTerms { get; set; } = new List<CourseTerm>();
}
