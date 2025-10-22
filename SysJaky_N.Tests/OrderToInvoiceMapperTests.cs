using System;
using System.ComponentModel.DataAnnotations;
using SysJaky_N.Models;
using SysJaky_N.Services.Pohoda;
using Xunit;

namespace SysJaky_N.Tests;

public class OrderToInvoiceMapperTests
{
    [Fact]
    public void Map_ProducesInvoiceWithHeaderAndItems()
    {
        var order = CreateSampleOrder();

        var invoice = OrderToInvoiceMapper.Map(order);

        Assert.NotNull(invoice.Header);
        Assert.Equal($"Objednávka {order.Id}", invoice.Header.Text);
        Assert.Equal(order.Items.Count, invoice.Items.Count);
        Assert.Equal(order.Total, invoice.TotalInclVat);
    }

    [Fact]
    public void Map_ThrowsWhenOrderHasNoItems()
    {
        var order = new Order
        {
            Id = 7,
            CreatedAt = DateTime.UtcNow,
            Total = 0m,
            TotalPrice = 0m
        };

        Assert.Throws<ValidationException>(() => OrderToInvoiceMapper.Map(order));
    }

    private static Order CreateSampleOrder()
    {
        var order = new Order
        {
            Id = 42,
            CreatedAt = new DateTime(2024, 5, 15, 10, 30, 0, DateTimeKind.Utc),
            PriceExclVat = 300m,
            Vat = 63m,
            Total = 363m,
            TotalPrice = 363m,
            PaymentConfirmation = "CONF123",
            InvoicePath = "INV-2024-001",
            UserId = "user-1"
        };

        order.Items.Add(new OrderItem
        {
            Id = 1,
            OrderId = 42,
            CourseId = 100,
            Course = new Course { Id = 100, Title = "Course A" },
            Quantity = 1,
            UnitPriceExclVat = 150m,
            Vat = 31.5m,
            Total = 181.5m
        });

        order.Items.Add(new OrderItem
        {
            Id = 2,
            OrderId = 42,
            CourseId = 200,
            Course = new Course { Id = 200, Title = "Course B" },
            Quantity = 2,
            UnitPriceExclVat = 75m,
            Vat = 31.5m,
            Total = 181.5m
        });

        return order;
    }
}
