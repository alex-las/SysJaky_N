using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SysJaky_N.Services.Pohoda;

public enum VatRate
{
    None,
    Low,
    High
}

public sealed record CustomerIdentity(
    [property: StringLength(255)] string? Company,
    [property: StringLength(255)] string? Name,
    [property: StringLength(255)] string? Street,
    [property: StringLength(255)] string? City,
    [property: StringLength(32)] string? Zip,
    [property: StringLength(64)] string? Country);

public sealed record InvoiceItem(
    [property: Required, StringLength(255)] string Name,
    [property: Range(1, int.MaxValue)] int Quantity,
    [property: Range(typeof(decimal), PohodaInvoiceValidationConstants.DecimalMinimum, PohodaInvoiceValidationConstants.DecimalMaximum)] decimal UnitPriceExclVat,
    [property: Range(typeof(decimal), PohodaInvoiceValidationConstants.DecimalMinimum, PohodaInvoiceValidationConstants.DecimalMaximum)] decimal TotalExclVat,
    [property: Range(typeof(decimal), PohodaInvoiceValidationConstants.DecimalMinimum, PohodaInvoiceValidationConstants.DecimalMaximum)] decimal VatAmount,
    [property: Range(typeof(decimal), PohodaInvoiceValidationConstants.DecimalMinimum, PohodaInvoiceValidationConstants.DecimalMaximum)] decimal TotalInclVat,
    [property: Range(typeof(decimal), PohodaInvoiceValidationConstants.DecimalMinimum, PohodaInvoiceValidationConstants.DecimalMaximum)] decimal Discount,
    VatRate Rate)
{
    public decimal DiscountPercentage => TotalInclVat == 0m
        ? 0m
        : Math.Round(Discount / TotalInclVat * 100m, 2, MidpointRounding.AwayFromZero);
}

public sealed record InvoiceHeader(
    [property: Required, StringLength(64)] string InvoiceType,
    [property: Required, StringLength(64)] string OrderNumber,
    [property: Required, StringLength(256)] string Text,
    [property: DataType(DataType.Date)] DateOnly Date,
    [property: DataType(DataType.Date)] DateOnly TaxDate,
    [property: DataType(DataType.Date)] DateOnly DueDate,
    [property: Required, StringLength(32)] string VariableSymbol,
    [property: StringLength(32)] string? SpecificSymbol,
    CustomerIdentity? Customer,
    [property: StringLength(512)] string? Note);

public sealed record InvoiceDto(
    [property: Required] InvoiceHeader Header,
    [property: Required] IReadOnlyList<InvoiceItem> Items,
    [property: Range(typeof(decimal), PohodaInvoiceValidationConstants.DecimalMinimum, PohodaInvoiceValidationConstants.DecimalMaximum)] decimal TotalExclVat,
    [property: Range(typeof(decimal), PohodaInvoiceValidationConstants.DecimalMinimum, PohodaInvoiceValidationConstants.DecimalMaximum)] decimal TotalVat,
    [property: Range(typeof(decimal), PohodaInvoiceValidationConstants.DecimalMinimum, PohodaInvoiceValidationConstants.DecimalMaximum)] decimal TotalInclVat,
    [property: Range(typeof(decimal), PohodaInvoiceValidationConstants.DecimalMinimum, PohodaInvoiceValidationConstants.DecimalMaximum)] decimal? NoneRateBase,
    [property: Range(typeof(decimal), PohodaInvoiceValidationConstants.DecimalMinimum, PohodaInvoiceValidationConstants.DecimalMaximum)] decimal? LowRateBase,
    [property: Range(typeof(decimal), PohodaInvoiceValidationConstants.DecimalMinimum, PohodaInvoiceValidationConstants.DecimalMaximum)] decimal? LowRateVat,
    [property: Range(typeof(decimal), PohodaInvoiceValidationConstants.DecimalMinimum, PohodaInvoiceValidationConstants.DecimalMaximum)] decimal? HighRateBase,
    [property: Range(typeof(decimal), PohodaInvoiceValidationConstants.DecimalMinimum, PohodaInvoiceValidationConstants.DecimalMaximum)] decimal? HighRateVat)
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

        var materializedItems = items as IList<InvoiceItem> ?? items.ToList();
        if (materializedItems.Count == 0)
        {
            throw new ValidationException("Invoice must contain at least one item.");
        }

        IReadOnlyList<InvoiceItem> readOnlyItems = materializedItems as IReadOnlyList<InvoiceItem>
            ?? new ReadOnlyCollection<InvoiceItem>(materializedItems.ToList());

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

internal static class PohodaInvoiceValidationConstants
{
    public const string DecimalMinimum = "0";
    public const string DecimalMaximum = "79228162514264337593543950335";
}
