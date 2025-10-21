using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models.Billing;

public sealed record VatSummary(
    [property: Range(0, double.MaxValue)] decimal TotalExclVat,
    [property: Range(0, double.MaxValue)] decimal TotalVat,
    [property: Range(0, double.MaxValue)] decimal TotalInclVat,
    [property: Range(0, double.MaxValue)] decimal NoneRateExclVat,
    [property: Range(0, double.MaxValue)] decimal NoneRateVat,
    [property: Range(0, double.MaxValue)] decimal LowRateExclVat,
    [property: Range(0, double.MaxValue)] decimal LowRateVat,
    [property: Range(0, double.MaxValue)] decimal HighRateExclVat,
    [property: Range(0, double.MaxValue)] decimal HighRateVat,
    [property: Range(0, double.MaxValue)] decimal DiscountTotal
);
