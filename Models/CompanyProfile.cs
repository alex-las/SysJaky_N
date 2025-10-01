using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class CompanyProfile
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Pole {0} je povinné.")]
    [StringLength(200, ErrorMessage = "Pole {0} může mít maximálně {1} znaků.")]
    [Display(Name = "Název")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Pole {0} je povinné.")]
    [StringLength(64, ErrorMessage = "Pole {0} může mít maximálně {1} znaků.")]
    [Display(Name = "Referenční kód")]
    public string ReferenceCode { get; set; } = string.Empty;

    [Display(Name = "Správce")]
    public string? ManagerId { get; set; }
    public ApplicationUser? Manager { get; set; }
    public List<ApplicationUser> Users { get; set; } = new();
}
