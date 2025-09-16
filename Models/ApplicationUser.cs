using Microsoft.AspNetCore.Identity;

namespace SysJaky_N.Models;

public class ApplicationUser : IdentityUser
{
    public int? CompanyProfileId { get; set; }
    public CompanyProfile? CompanyProfile { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<WaitlistEntry> WaitlistEntries { get; set; } = new List<WaitlistEntry>();
    public ICollection<CompanyUser> CompanyMemberships { get; set; } = new List<CompanyUser>();
}
