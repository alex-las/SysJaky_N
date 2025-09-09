using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class Order
{
    public int Id { get; set; }

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public List<OrderItem> Items { get; set; } = new();

    public OrderStatus Status { get; set; } = OrderStatus.Created;

    [DataType(DataType.Currency)]
    public decimal TotalPrice { get; set; }

    [DataType(DataType.DateTime)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int CourseId { get; set; }
    public Course? Course { get; set; }

    public int Quantity { get; set; }
}

public enum OrderStatus
{
    Created,
    Paid,
    Cancelled
}
