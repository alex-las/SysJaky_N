using System;
using System.Linq;
using SysJaky_N.Models;
using SysJaky_N.Services.Pohoda;
using Xunit;

namespace SysJaky_N.Tests;

public class PohodaOrderPayloadTests
{
    [Fact]
    public void FromOrder_BuildsDokladAndItems()
    {
        var order = new Order
        {
            Id = 42,
            UserId = "user-1",
            CreatedAt = new DateTime(2024, 5, 15, 10, 30, 0, DateTimeKind.Utc),
            PriceExclVat = 300m,
            Vat = 63m,
            Total = 363m,
            TotalPrice = 363m,
            PaymentConfirmation = "CONF123",
            InvoicePath = "INV-2024-001"
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

        var payload = PohodaOrderPayload.FromOrder(order);

        Assert.Equal(order.Id, payload.Doklad.OrderId);
        Assert.Equal(order.UserId, payload.Doklad.CustomerId);
        Assert.Equal(order.CreatedAt, payload.Doklad.CreatedAt);
        Assert.Equal(order.PriceExclVat, payload.Doklad.PriceExclVat);
        Assert.Equal(order.Vat, payload.Doklad.Vat);
        Assert.Equal(order.Total, payload.Doklad.TotalInclVat);
        Assert.Equal(0m, payload.Doklad.Discount);
        Assert.Equal(order.PaymentConfirmation, payload.Doklad.PaymentReference);
        Assert.Equal(order.InvoicePath, payload.Doklad.InvoiceNumber);

        Assert.Equal(2, payload.Polozky.Count);

        var firstItem = payload.Polozky.First(p => p.OrderItemId == 1);
        Assert.Equal("Course A", firstItem.Name);
        Assert.Equal(1, firstItem.Quantity);
        Assert.Equal(150m, firstItem.UnitPriceExclVat);
        Assert.Equal(181.5m, firstItem.TotalInclVat);
        Assert.Equal(31.5m, firstItem.VatAmount);
        Assert.Equal(150m, firstItem.TotalExclVat);
        Assert.Equal(21m, firstItem.VatRate);
        Assert.Equal(0m, firstItem.Discount);

        var secondItem = payload.Polozky.First(p => p.OrderItemId == 2);
        Assert.Equal("Course B", secondItem.Name);
        Assert.Equal(2, secondItem.Quantity);
        Assert.Equal(75m, secondItem.UnitPriceExclVat);
        Assert.Equal(181.5m, secondItem.TotalInclVat);
        Assert.Equal(31.5m, secondItem.VatAmount);
        Assert.Equal(150m, secondItem.TotalExclVat);
        Assert.Equal(21m, secondItem.VatRate);
        Assert.Equal(0m, secondItem.Discount);
    }

    [Fact]
    public void FromOrder_DistributesDiscountAcrossItems()
    {
        var order = new Order
        {
            Id = 7,
            UserId = "customer-5",
            CreatedAt = new DateTime(2024, 6, 1, 8, 0, 0, DateTimeKind.Utc),
            PriceExclVat = 300m,
            Vat = 63m,
            Total = 363m,
            TotalPrice = 400m
        };

        order.Items.Add(new OrderItem
        {
            Id = 11,
            OrderId = 7,
            CourseId = 10,
            Course = new Course { Id = 10, Title = "VAT Basics" },
            Quantity = 1,
            UnitPriceExclVat = 100m,
            Vat = 21m,
            Total = 121m
        });

        order.Items.Add(new OrderItem
        {
            Id = 12,
            OrderId = 7,
            CourseId = 20,
            Course = new Course { Id = 20, Title = "Advanced VAT" },
            Quantity = 2,
            UnitPriceExclVat = 100m,
            Vat = 42m,
            Total = 242m
        });

        var payload = PohodaOrderPayload.FromOrder(order);

        Assert.Equal(37m, payload.Doklad.Discount);
        Assert.Equal(2, payload.Polozky.Count);

        var firstItem = payload.Polozky.First(p => p.OrderItemId == 11);
        var secondItem = payload.Polozky.First(p => p.OrderItemId == 12);

        Assert.Equal(12.33m, firstItem.Discount);
        Assert.Equal(24.67m, secondItem.Discount);
        Assert.Equal(37m, Math.Round(firstItem.Discount + secondItem.Discount, 2, MidpointRounding.AwayFromZero));

        Assert.Equal(21m, firstItem.VatRate);
        Assert.Equal(21m, secondItem.VatRate);
        Assert.Equal(121m, firstItem.TotalInclVat);
        Assert.Equal(242m, secondItem.TotalInclVat);
        Assert.All(payload.Polozky, item => Assert.Equal(7, item.OrderId));
    }
}
