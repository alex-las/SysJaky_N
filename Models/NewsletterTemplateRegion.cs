using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class NewsletterTemplateRegion
{
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }

    public int NewsletterTemplateId { get; set; }

    public NewsletterTemplate NewsletterTemplate { get; set; } = default!;

    [Required]
    public int NewsletterSectionCategoryId { get; set; }

    public NewsletterSectionCategory Category { get; set; } = default!;

    public ICollection<NewsletterIssueSection> IssueSections { get; set; } = new List<NewsletterIssueSection>();
}
