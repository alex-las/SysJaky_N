using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class NewsletterIssueDelivery
{
    public int Id { get; set; }

    [Required]
    public int NewsletterIssueId { get; set; }

    public NewsletterIssue? NewsletterIssue { get; set; }

    [Required]
    public int NewsletterSubscriberId { get; set; }

    public NewsletterSubscriber? NewsletterSubscriber { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string RecipientEmail { get; set; } = string.Empty;

    public string? RenderedHtml { get; set; }

    public DateTime SentUtc { get; set; }

    [MaxLength(256)]
    public string Status { get; set; } = string.Empty;

    public int? EmailLogId { get; set; }

    public EmailLog? EmailLog { get; set; }
}
