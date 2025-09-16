using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class SeatToken
{
    public int Id { get; set; }

    public int OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    [MaxLength(64)]
    public string Token { get; set; } = string.Empty;

    public string? RedeemedByUserId { get; set; }
    public ApplicationUser? RedeemedByUser { get; set; }

    public DateTime? RedeemedAtUtc { get; set; }
}
