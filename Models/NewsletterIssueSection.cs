namespace SysJaky_N.Models;

public class NewsletterIssueSection
{
    public int NewsletterIssueId { get; set; }

    public NewsletterIssue NewsletterIssue { get; set; } = default!;

    public int NewsletterSectionId { get; set; }

    public NewsletterSection NewsletterSection { get; set; } = default!;

    public int? NewsletterTemplateRegionId { get; set; }

    public NewsletterTemplateRegion? TemplateRegion { get; set; }

    public int SortOrder { get; set; }
}
