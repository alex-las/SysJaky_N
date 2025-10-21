using System;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models.Billing;

public sealed record InvoiceItem(
    [property: Required, StringLength(255)] string Name,
    [property: Range(1, int.MaxValue)] int Quantity,
    [property: Range(typeof(decimal), BillingValidationConstants.DecimalMinimum, BillingValidationConstants.DecimalMaximum)] decimal UnitPriceExclVat,
    [property: Range(typeof(decimal), BillingValidationConstants.DecimalMinimum, BillingValidationConstants.DecimalMaximum)] decimal TotalExclVat,
    [property: Range(typeof(decimal), BillingValidationConstants.DecimalMinimum, BillingValidationConstants.DecimalMaximum)] decimal VatAmount,
    [property: Range(typeof(decimal), BillingValidationConstants.DecimalMinimum, BillingValidationConstants.DecimalMaximum)] decimal TotalInclVat,
    [property: Range(typeof(decimal), BillingValidationConstants.DecimalMinimum, BillingValidationConstants.DecimalMaximum)] decimal Discount,
    VatRate Rate)
{
    public decimal DiscountPercentage => TotalInclVat == 0m
        ? 0m
        : Math.Round(Discount / TotalInclVat * 100m, 2, MidpointRounding.AwayFromZero);
}

public enum VatRate
{
    None,
    Low,
    High
}
