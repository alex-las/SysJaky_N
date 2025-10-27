using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Services.Pohoda;

public enum VatRate
{
    High,
    Low,
    None
}

public sealed record CustomerIdentity(
    string? Company,
    string? Name,
    string? Street,
    string? City,
    string? Zip,
    string? Country);

public sealed record InvoiceItem(
    string Name,
    int Quantity,
    decimal UnitPriceExclVat,
    decimal TotalExclVat,
    decimal VatAmount,
    decimal TotalInclVat,
    decimal Discount,
    VatRate Rate)
{
    public decimal DiscountPercentage => TotalInclVat == 0m
        ? 0m
        : Math.Round(Discount / TotalInclVat * 100m, 2, MidpointRounding.AwayFromZero);
}

public sealed record InvoiceHeader(
    string InvoiceType,
    string OrderNumber,
    string Text,
    DateOnly Date,
    DateOnly TaxDate,
    DateOnly DueDate,
    string VariableSymbol,
    string? SpecificSymbol,
    CustomerIdentity? Customer,
    string? Note);

public sealed record InvoiceDto(
    InvoiceHeader Header,
    IReadOnlyList<InvoiceItem> Items,
    decimal TotalExclVat,
    decimal TotalVat,
    decimal TotalInclVat,
    decimal? NoneRateBase,
    decimal? LowRateBase,
    decimal? LowRateVat,
    decimal? HighRateBase,
    decimal? HighRateVat)
{
    public static InvoiceDto Create(
        InvoiceHeader header,
        IEnumerable<InvoiceItem> items,
        decimal totalExclVat,
        decimal totalVat,
        decimal totalInclVat,
        decimal? noneRateBase,
        decimal? lowRateBase,
        decimal? lowRateVat,
        decimal? highRateBase,
        decimal? highRateVat)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(items);

        var materializedItems = items as IList<InvoiceItem> ?? new List<InvoiceItem>(items);
        if (materializedItems.Count == 0)
        {
            throw new ValidationException("Invoice must contain at least one item.");
        }

        IReadOnlyList<InvoiceItem> readOnlyItems = materializedItems as IReadOnlyList<InvoiceItem>
            ?? new ReadOnlyCollection<InvoiceItem>(new List<InvoiceItem>(materializedItems));

        return new InvoiceDto(
            header,
            readOnlyItems,
            totalExclVat,
            totalVat,
            totalInclVat,
            noneRateBase,
            lowRateBase,
            lowRateVat,
            highRateBase,
            highRateVat);
    }
}
