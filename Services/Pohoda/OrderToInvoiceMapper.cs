using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using SysJaky_N.Models;
using SysJaky_N.Models.Billing;

namespace SysJaky_N.Services.Pohoda;

public static class OrderToInvoiceMapper
{
    public static Invoice Map(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var header = BuildHeader(order);
        var items = MapItems(order);
        if (items.Count == 0)
        {
            throw new ValidationException("Invoice must contain at least one item.");
        }

        var summary = BuildSummary(items, order);
        var invoice = new Invoice(header, items, summary);
        Validate(invoice);
        return invoice;
    }

    private static InvoiceHeader BuildHeader(Order order)
    {
        var createdAt = order.CreatedAt == default
            ? DateTime.UtcNow
            : order.CreatedAt;

        var customer = string.IsNullOrWhiteSpace(order.UserId)
            ? null
            : new CustomerIdentity(order.UserId, null, null, null, null, null);

        return new InvoiceHeader(
            InvoiceType: "issuedInvoice",
            OrderNumber: order.Id.ToString(CultureInfo.InvariantCulture),
            Text: $"Objednávka {order.Id}",
            Date: DateOnly.FromDateTime(createdAt),
            TaxDate: DateOnly.FromDateTime(createdAt),
            DueDate: DateOnly.FromDateTime(createdAt.AddDays(14)),
            VariableSymbol: order.Id.ToString(CultureInfo.InvariantCulture),
            SpecificSymbol: string.IsNullOrWhiteSpace(order.PaymentConfirmation) ? null : order.PaymentConfirmation,
            Customer: customer,
            Note: string.IsNullOrWhiteSpace(order.InvoicePath) ? null : order.InvoicePath);
    }

    private static IReadOnlyList<InvoiceItem> MapItems(Order order)
    {
        var orderItems = order.Items ?? new List<OrderItem>();
        if (orderItems.Count == 0)
        {
            return Array.Empty<InvoiceItem>();
        }

        var discountTotal = Math.Max(order.TotalPrice - order.Total, 0m);
        var roundedItems = orderItems
            .Select(item => new
            {
                Item = item,
                Total = RoundCurrency(item.Total),
                Vat = RoundCurrency(item.Vat)
            })
            .ToList();

        var grossSum = roundedItems.Sum(i => i.Total);
        var mapped = new List<InvoiceItem>(roundedItems.Count);
        decimal allocatedDiscount = 0m;

        for (var i = 0; i < roundedItems.Count; i++)
        {
            var current = roundedItems[i];
            var item = current.Item;
            var totalInclVat = current.Total;
            var vatAmount = current.Vat;
            var totalExclVat = RoundCurrency(totalInclVat - vatAmount);
            var rate = DetermineVatRate(totalExclVat, vatAmount);

            var itemDiscount = 0m;
            if (discountTotal > 0m && grossSum > 0m)
            {
                if (i == roundedItems.Count - 1)
                {
                    itemDiscount = RoundCurrency(discountTotal - allocatedDiscount);
                }
                else
                {
                    var ratio = totalInclVat / grossSum;
                    itemDiscount = RoundCurrency(discountTotal * ratio);
                    allocatedDiscount += itemDiscount;
                }
            }

            var quantity = Math.Max(1, item.Quantity);
            var unitPriceExclVat = quantity == 0
                ? totalExclVat
                : RoundCurrency(item.UnitPriceExclVat);

            var invoiceItem = new InvoiceItem(
                Name: item.Course?.Title ?? $"Course #{item.CourseId}",
                Quantity: quantity,
                UnitPriceExclVat: unitPriceExclVat,
                TotalExclVat: totalExclVat,
                VatAmount: vatAmount,
                TotalInclVat: totalInclVat,
                Discount: itemDiscount,
                Rate: rate);

            mapped.Add(invoiceItem);
        }

        if (discountTotal > 0m && mapped.Count > 0)
        {
            var sum = mapped.Sum(p => p.Discount);
            var delta = RoundCurrency(discountTotal - sum);
            if (delta != 0m)
            {
                var last = mapped[^1];
                mapped[^1] = last with { Discount = RoundCurrency(last.Discount + delta) };
            }
        }

        return mapped;
    }

    private static VatSummary BuildSummary(IReadOnlyList<InvoiceItem> items, Order order)
    {
        var totals = items.Aggregate(new SummaryTotals(), (acc, item) => acc with
        {
            TotalExclVat = acc.TotalExclVat + item.TotalExclVat,
            TotalVat = acc.TotalVat + item.VatAmount,
            HighRateBase = acc.HighRateBase + (item.Rate == VatRate.High ? item.TotalExclVat : 0m),
            HighRateVat = acc.HighRateVat + (item.Rate == VatRate.High ? item.VatAmount : 0m),
            LowRateBase = acc.LowRateBase + (item.Rate == VatRate.Low ? item.TotalExclVat : 0m),
            LowRateVat = acc.LowRateVat + (item.Rate == VatRate.Low ? item.VatAmount : 0m),
            NoneRateBase = acc.NoneRateBase + (item.Rate == VatRate.None ? item.TotalExclVat : 0m)
        });

        var totalInclVat = RoundCurrency(order.Total);

        return new VatSummary(
            TotalExclVat: RoundCurrency(totals.TotalExclVat),
            TotalVat: RoundCurrency(totals.TotalVat),
            TotalInclVat: totalInclVat,
            NoneRateBase: totals.NoneRateBase > 0m ? RoundCurrency(totals.NoneRateBase) : null,
            LowRateBase: totals.LowRateBase > 0m ? RoundCurrency(totals.LowRateBase) : null,
            LowRateVat: totals.LowRateVat > 0m ? RoundCurrency(totals.LowRateVat) : null,
            HighRateBase: totals.HighRateBase > 0m ? RoundCurrency(totals.HighRateBase) : null,
            HighRateVat: totals.HighRateVat > 0m ? RoundCurrency(totals.HighRateVat) : null);
    }

    private static void Validate(Invoice invoice)
    {
        ValidateObject(invoice);
        ValidateObject(invoice.Header);
        if (invoice.Header.Customer is not null)
        {
            ValidateObject(invoice.Header.Customer);
        }

        foreach (var item in invoice.Items)
        {
            ValidateObject(item);
        }

        if (invoice.Items.Count == 0)
        {
            throw new ValidationException("Invoice must contain at least one item.");
        }

        ValidateObject(invoice.Summary);
    }

    private static void ValidateObject(object instance)
    {
        var context = new ValidationContext(instance);
        Validator.ValidateObject(instance, context, validateAllProperties: true);
    }

    private static VatRate DetermineVatRate(decimal baseAmount, decimal vatAmount)
    {
        if (baseAmount == 0m)
        {
            return VatRate.None;
        }

        var rate = vatAmount / baseAmount * 100m;
        if (rate >= 20m)
        {
            return VatRate.High;
        }

        if (rate >= 10m)
        {
            return VatRate.Low;
        }

        return VatRate.None;
    }

    private static decimal RoundCurrency(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record SummaryTotals
    {
        public decimal TotalExclVat { get; init; }
        public decimal TotalVat { get; init; }
        public decimal HighRateBase { get; init; }
        public decimal HighRateVat { get; init; }
        public decimal LowRateBase { get; init; }
        public decimal LowRateVat { get; init; }
        public decimal NoneRateBase { get; init; }
    }
}
