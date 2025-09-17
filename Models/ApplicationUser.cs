using System;
using Microsoft.AspNetCore.Identity;

namespace SysJaky_N.Models;

public class ApplicationUser : IdentityUser
{
    public int? CompanyProfileId { get; set; }
    public CompanyProfile? CompanyProfile { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<WaitlistEntry> WaitlistEntries { get; set; } = new List<WaitlistEntry>();
    public ICollection<CompanyUser> CompanyMemberships { get; set; } = new List<CompanyUser>();
    public ICollection<LessonProgress> LessonProgresses { get; set; } = new List<LessonProgress>();

    public bool PersonalDataProcessingConsent { get; set; }
    public DateTime? PersonalDataProcessingConsentUpdatedAtUtc { get; set; }

    public bool MarketingEmailsEnabled { get; set; } = true;
    public DateTime? MarketingConsentUpdatedAtUtc { get; set; }
}
