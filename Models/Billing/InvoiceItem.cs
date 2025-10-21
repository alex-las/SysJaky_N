using System;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models.Billing;

public sealed record InvoiceItem(
    [property: Required, StringLength(256)] string Name,
    [property: Range(1, int.MaxValue)] int Quantity,
    [property: Range(0, double.MaxValue)] decimal UnitPriceExclVat,
    [property: Range(0, double.MaxValue)] decimal TotalExclVat,
    [property: Range(0, double.MaxValue)] decimal VatAmount,
    [property: Range(0, double.MaxValue)] decimal TotalInclVat,
    [property: Range(0, double.MaxValue)] decimal Discount,
    [property: Required] VatRate Rate)
{
    public decimal DiscountPercentage => TotalInclVat == 0m
        ? 0m
        : Math.Round(Discount / TotalInclVat * 100m, 2, MidpointRounding.AwayFromZero);
}
