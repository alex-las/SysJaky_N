using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class CompanyProfile
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(200, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Models.CompanyProfile.Name.DisplayName")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(64, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Models.CompanyProfile.ReferenceCode.DisplayName")]
    public string ReferenceCode { get; set; } = string.Empty;

    [Display(Name = "Models.CompanyProfile.ManagerId.DisplayName")]
    public string? ManagerId { get; set; }
    public ApplicationUser? Manager { get; set; }
    public List<ApplicationUser> Users { get; set; } = new();
}
