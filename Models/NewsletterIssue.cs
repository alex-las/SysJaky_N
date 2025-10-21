using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class NewsletterIssue
{
    public int Id { get; set; }

    [Required]
    [MaxLength(180)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(180)]
    public string? Preheader { get; set; }

    public string? IntroHtml { get; set; }

    public string? OutroHtml { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ScheduledForUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public ICollection<NewsletterIssueSection> Sections { get; set; } = new List<NewsletterIssueSection>();

    public ICollection<NewsletterIssueCategory> Categories { get; set; } = new List<NewsletterIssueCategory>();
}
