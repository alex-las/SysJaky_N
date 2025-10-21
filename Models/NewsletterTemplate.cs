using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class NewsletterTemplate
{
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(16)]
    public string PrimaryColor { get; set; } = "#2563eb";

    [MaxLength(16)]
    public string SecondaryColor { get; set; } = "#facc15";

    [MaxLength(16)]
    public string BackgroundColor { get; set; } = "#f9fafb";

    [Required]
    public string BaseLayoutHtml { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<NewsletterTemplateRegion> Regions { get; set; } = new List<NewsletterTemplateRegion>();
}
