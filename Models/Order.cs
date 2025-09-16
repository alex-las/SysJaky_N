using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class Order
{
    public int Id { get; set; }

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public List<OrderItem> Items { get; set; } = new();

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [DataType(DataType.Currency)]
    public decimal PriceExclVat { get; set; }

    [DataType(DataType.Currency)]
    public decimal Vat { get; set; }

    [DataType(DataType.Currency)]
    public decimal Total { get; set; }

    [DataType(DataType.Currency)]
    public decimal TotalPrice { get; set; }

    public int? DiscountCodeId { get; set; }
    public DiscountCode? DiscountCode { get; set; }

    public string? PaymentConfirmation { get; set; }

    public string? InvoicePath { get; set; }

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

    [DataType(DataType.Currency)]
    public decimal UnitPriceExclVat { get; set; }

    [DataType(DataType.Currency)]
    public decimal Vat { get; set; }

    [DataType(DataType.Currency)]
    public decimal Total { get; set; }
}

public enum OrderStatus
{
    Pending,
    Paid,
    Cancelled,
    Refunded
}
