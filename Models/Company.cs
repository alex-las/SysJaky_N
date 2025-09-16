using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class Company
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string ReferralCode { get; set; } = string.Empty;

    public ICollection<CompanyUser> Users { get; set; } = new List<CompanyUser>();
}
