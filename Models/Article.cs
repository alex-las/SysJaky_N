using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class Article
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [DataType(DataType.DateTime)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsPublished { get; set; }

    [DataType(DataType.DateTime)]
    public DateTime? PublishedAtUtc { get; set; }

    [DataType(DataType.DateTime)]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? AuthorId { get; set; }

    public ApplicationUser? Author { get; set; }
}
