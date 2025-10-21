using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models.Billing;

public sealed record VatSummary(
    [property: Range(typeof(decimal), BillingValidationConstants.DecimalMinimum, BillingValidationConstants.DecimalMaximum)] decimal TotalExclVat,
    [property: Range(typeof(decimal), BillingValidationConstants.DecimalMinimum, BillingValidationConstants.DecimalMaximum)] decimal TotalVat,
    [property: Range(typeof(decimal), BillingValidationConstants.DecimalMinimum, BillingValidationConstants.DecimalMaximum)] decimal TotalInclVat,
    [property: Range(typeof(decimal), BillingValidationConstants.DecimalMinimum, BillingValidationConstants.DecimalMaximum)] decimal? NoneRateBase,
    [property: Range(typeof(decimal), BillingValidationConstants.DecimalMinimum, BillingValidationConstants.DecimalMaximum)] decimal? LowRateBase,
    [property: Range(typeof(decimal), BillingValidationConstants.DecimalMinimum, BillingValidationConstants.DecimalMaximum)] decimal? LowRateVat,
    [property: Range(typeof(decimal), BillingValidationConstants.DecimalMinimum, BillingValidationConstants.DecimalMaximum)] decimal? HighRateBase,
    [property: Range(typeof(decimal), BillingValidationConstants.DecimalMinimum, BillingValidationConstants.DecimalMaximum)] decimal? HighRateVat);
