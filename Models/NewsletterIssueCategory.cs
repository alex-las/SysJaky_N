namespace SysJaky_N.Models;

public class NewsletterIssueCategory
{
    public int NewsletterIssueId { get; set; }

    public NewsletterIssue NewsletterIssue { get; set; } = default!;

    public int NewsletterSectionCategoryId { get; set; }

    public NewsletterSectionCategory NewsletterSectionCategory { get; set; } = default!;
}
