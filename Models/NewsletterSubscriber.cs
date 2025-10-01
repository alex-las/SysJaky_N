using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class NewsletterSubscriber
{
    public int Id { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    public DateTime SubscribedAtUtc { get; set; }

    public DateTime? ConfirmedAtUtc { get; set; }

    [Required]
    [MaxLength(64)]
    public string ConfirmationToken { get; set; } = string.Empty;

    public bool ConsentGiven { get; set; }

    public DateTime? ConsentGivenAtUtc { get; set; }
}
