using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class NewsletterSectionCategory
{
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Description { get; set; }

    public int? CourseCategoryId { get; set; }

    public CourseCategory? CourseCategory { get; set; }

    public ICollection<NewsletterSection> Sections { get; set; } = new List<NewsletterSection>();

    public ICollection<NewsletterIssueCategory> IssueCategories { get; set; } = new List<NewsletterIssueCategory>();
}
