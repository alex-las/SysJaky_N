using System;
using System.Collections.Generic;
using System.Linq;
using SysJaky_N.Models;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaOrderPayload
{
    private PohodaOrderPayload(PohodaDokladRecord doklad, IReadOnlyList<PohodaPolozkaRecord> polozky)
    {
        Doklad = doklad;
        Polozky = polozky;
    }

    public PohodaDokladRecord Doklad { get; }

    public IReadOnlyList<PohodaPolozkaRecord> Polozky { get; }

    public static PohodaOrderPayload FromOrder(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var discount = RoundCurrency(Math.Max(order.TotalPrice - order.Total, 0m));
        var doklad = new PohodaDokladRecord
        {
            OrderId = order.Id,
            ExternalOrderNumber = order.Id.ToString(),
            CustomerId = order.UserId,
            CreatedAt = order.CreatedAt,
            PriceExclVat = RoundCurrency(order.PriceExclVat),
            Vat = RoundCurrency(order.Vat),
            TotalInclVat = RoundCurrency(order.Total),
            Discount = discount,
            PaymentReference = order.PaymentConfirmation,
            InvoiceNumber = order.InvoicePath
        };

        var polozky = MapOrderItems(order, discount);
        return new PohodaOrderPayload(doklad, polozky);
    }

    private static IReadOnlyList<PohodaPolozkaRecord> MapOrderItems(Order order, decimal totalDiscount)
    {
        var items = order.Items ?? new List<OrderItem>();
        if (items.Count == 0)
        {
            return Array.Empty<PohodaPolozkaRecord>();
        }

        var roundedItems = items
            .Select(item => new
            {
                Item = item,
                Total = RoundCurrency(item.Total),
                Vat = RoundCurrency(item.Vat)
            })
            .ToList();

        var grossSum = roundedItems.Sum(i => i.Total);
        var polozky = new List<PohodaPolozkaRecord>(roundedItems.Count);
        decimal allocatedDiscount = 0m;

        for (var index = 0; index < roundedItems.Count; index++)
        {
            var rounded = roundedItems[index];
            var item = rounded.Item;
            var quantity = item.Quantity;
            var totalInclVat = rounded.Total;
            var vatAmount = rounded.Vat;
            var totalExclVat = RoundCurrency(totalInclVat - vatAmount);
            var vatRate = totalExclVat == 0m
                ? 0m
                : Math.Round(vatAmount / totalExclVat * 100m, 2, MidpointRounding.AwayFromZero);

            var itemDiscount = 0m;
            if (totalDiscount > 0m && grossSum > 0m)
            {
                if (index == roundedItems.Count - 1)
                {
                    itemDiscount = RoundCurrency(totalDiscount - allocatedDiscount);
                }
                else
                {
                    var ratio = totalInclVat / grossSum;
                    itemDiscount = RoundCurrency(totalDiscount * ratio);
                    allocatedDiscount += itemDiscount;
                }
            }

            var polozka = new PohodaPolozkaRecord
            {
                OrderItemId = item.Id,
                OrderId = order.Id,
                CourseId = item.CourseId,
                Name = item.Course?.Title ?? $"Course #{item.CourseId}",
                Quantity = quantity,
                UnitPriceExclVat = RoundCurrency(item.UnitPriceExclVat),
                VatRate = vatRate,
                TotalExclVat = totalExclVat,
                VatAmount = vatAmount,
                TotalInclVat = totalInclVat,
                Discount = itemDiscount
            };

            polozky.Add(polozka);
        }

        if (totalDiscount > 0m)
        {
            var sum = polozky.Sum(p => p.Discount);
            var delta = RoundCurrency(totalDiscount - sum);
            if (delta != 0m && polozky.Count > 0)
            {
                polozky[^1].Discount = RoundCurrency(polozky[^1].Discount + delta);
            }
        }

        return polozky;
    }

    private static decimal RoundCurrency(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed class PohodaDokladRecord
{
    public int OrderId { get; set; }

    public string? ExternalOrderNumber { get; set; }

    public string? CustomerId { get; set; }

    public DateTime CreatedAt { get; set; }

    public decimal PriceExclVat { get; set; }

    public decimal Vat { get; set; }

    public decimal TotalInclVat { get; set; }

    public decimal Discount { get; set; }

    public string? PaymentReference { get; set; }

    public string? InvoiceNumber { get; set; }
}

public sealed class PohodaPolozkaRecord
{
    public int OrderItemId { get; set; }

    public int OrderId { get; set; }

    public int CourseId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPriceExclVat { get; set; }

    public decimal VatRate { get; set; }

    public decimal TotalExclVat { get; set; }

    public decimal VatAmount { get; set; }

    public decimal TotalInclVat { get; set; }

    public decimal Discount { get; set; }

    public int? DokladId { get; set; }
}
