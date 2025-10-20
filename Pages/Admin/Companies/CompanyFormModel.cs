using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SysJaky_N.Pages.Admin.Companies;

public class CompanyFormModel
{
    public int? Id { get; set; }

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

    public IEnumerable<SelectListItem> ManagerOptions { get; set; } = Enumerable.Empty<SelectListItem>();
}
