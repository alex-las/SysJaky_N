using System;
using System.Collections.Generic;
using System.Globalization;
using SysJaky_N.Models;
using SysJaky_N.Models.Billing;

namespace SysJaky_N.Services.Pohoda;

public sealed class OrderToInvoiceMapper
{
    public Invoice Map(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var items = MapItems(order);
        var summary = BuildSummary(order, items);
        var header = BuildHeader(order);
        var externalId = order.Id.ToString(CultureInfo.InvariantCulture);

        return new Invoice(externalId, header, items, summary);
    }

    private static InvoiceHeader BuildHeader(Order order)
    {
        var issueDate = DateOnly.FromDateTime(order.CreatedAt);
        var taxDate = issueDate;
        var dueDate = issueDate.AddDays(14);
        var orderNumber = order.Id.ToString(CultureInfo.InvariantCulture);

        return new InvoiceHeader(
            Type: InvoiceType.IssuedInvoice,
            IssueDate: issueDate,
            TaxDate: taxDate,
            DueDate: dueDate,
            OrderNumber: orderNumber,
            Text: $"Objednávka {orderNumber}",
            VariableSymbol: orderNumber,
            SpecificSymbol: string.IsNullOrWhiteSpace(order.PaymentConfirmation) ? null : order.PaymentConfirmation,
            Note: string.IsNullOrWhiteSpace(order.InvoicePath) ? null : order.InvoicePath,
            Customer: MapCustomer(order));
    }

    private static CustomerIdentity? MapCustomer(Order order)
    {
        var companyName = order.Customer?.CompanyProfile?.Name;
        if (string.IsNullOrWhiteSpace(companyName))
        {
            companyName = order.UserId;
        }

        var contactName = order.Customer?.UserName;
        var email = order.Customer?.Email;
        var phone = order.Customer?.PhoneNumber;

        if (string.IsNullOrWhiteSpace(companyName) && string.IsNullOrWhiteSpace(contactName) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        return new CustomerIdentity(
            Company: companyName,
            ContactName: contactName,
            Email: email,
            Phone: phone);
    }

    private static IReadOnlyList<InvoiceItem> MapItems(Order order)
    {
        var items = order.Items ?? new List<OrderItem>();
        if (items.Count == 0)
        {
            return Array.Empty<InvoiceItem>();
        }

        var discountTotal = Math.Max(order.TotalPrice - order.Total, 0m);
        var roundedItems = new List<(OrderItem Item, decimal Total, decimal Vat)>(items.Count);

        foreach (var item in items)
        {
            roundedItems.Add((item, RoundCurrency(item.Total), RoundCurrency(item.Vat)));
        }

        var grossSum = 0m;
        foreach (var entry in roundedItems)
        {
            grossSum += entry.Total;
        }

        var mapped = new List<InvoiceItem>(roundedItems.Count);
        var allocatedDiscount = 0m;

        for (var i = 0; i < roundedItems.Count; i++)
        {
            var current = roundedItems[i];
            var orderItem = current.Item;
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

            var quantity = Math.Max(1, orderItem.Quantity);
            var unitPriceExclVat = quantity == 0
                ? totalExclVat
                : RoundCurrency(orderItem.UnitPriceExclVat);

            var name = orderItem.Course?.Title ?? $"Course #{orderItem.CourseId}";

            mapped.Add(new InvoiceItem(
                Name: name,
                Quantity: quantity,
                UnitPriceExclVat: unitPriceExclVat,
                TotalExclVat: totalExclVat,
                VatAmount: vatAmount,
                TotalInclVat: totalInclVat,
                Discount: itemDiscount,
                Rate: rate));
        }

        if (discountTotal > 0m && mapped.Count > 0)
        {
            var sum = 0m;
            foreach (var item in mapped)
            {
                sum += item.Discount;
            }

            var delta = RoundCurrency(discountTotal - sum);
            if (delta != 0m)
            {
                var last = mapped[^1];
                mapped[^1] = last with { Discount = RoundCurrency(last.Discount + delta) };
            }
        }

        return mapped;
    }

    private static VatSummary BuildSummary(Order order, IReadOnlyList<InvoiceItem> items)
    {
        decimal totalExclVat = 0m;
        decimal totalVat = 0m;
        decimal noneExclVat = 0m;
        decimal noneVat = 0m;
        decimal lowExclVat = 0m;
        decimal lowVat = 0m;
        decimal highExclVat = 0m;
        decimal highVat = 0m;
        decimal discountTotal = 0m;

        foreach (var item in items)
        {
            totalExclVat += item.TotalExclVat;
            totalVat += item.VatAmount;
            discountTotal += item.Discount;

            switch (item.Rate)
            {
                case VatRate.High:
                    highExclVat += item.TotalExclVat;
                    highVat += item.VatAmount;
                    break;
                case VatRate.Low:
                    lowExclVat += item.TotalExclVat;
                    lowVat += item.VatAmount;
                    break;
                default:
                    noneExclVat += item.TotalExclVat;
                    noneVat += item.VatAmount;
                    break;
            }
        }

        return new VatSummary(
            TotalExclVat: RoundCurrency(totalExclVat),
            TotalVat: RoundCurrency(totalVat),
            TotalInclVat: RoundCurrency(order.Total),
            NoneRateExclVat: RoundCurrency(noneExclVat),
            NoneRateVat: RoundCurrency(noneVat),
            LowRateExclVat: RoundCurrency(lowExclVat),
            LowRateVat: RoundCurrency(lowVat),
            HighRateExclVat: RoundCurrency(highExclVat),
            HighRateVat: RoundCurrency(highVat),
            DiscountTotal: RoundCurrency(discountTotal));
    }

    private static VatRate DetermineVatRate(decimal baseAmount, decimal vatAmount)
    {
        if (baseAmount == 0m)
        {
            return VatRate.None;
        }

        var rate = baseAmount == 0m ? 0m : vatAmount / baseAmount * 100m;
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
}
