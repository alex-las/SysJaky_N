using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class NewsletterSection
{
    public int Id { get; set; }

    [Required]
    [MaxLength(180)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string HtmlContent { get; set; } = string.Empty;

    public bool IsPublished { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public int NewsletterSectionCategoryId { get; set; }

    public NewsletterSectionCategory Category { get; set; } = default!;

    public ICollection<NewsletterIssueSection> IssueSections { get; set; } = new List<NewsletterIssueSection>();
}
