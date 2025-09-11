using Microsoft.AspNetCore.Identity;

namespace SysJaky_N.Models;

public class ApplicationUser : IdentityUser
{
    public int? CompanyProfileId { get; set; }
    public CompanyProfile? CompanyProfile { get; set; }
}
