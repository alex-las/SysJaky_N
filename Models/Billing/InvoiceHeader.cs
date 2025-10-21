using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models.Billing;

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
    [property: StringLength(512)] string? Note)
{
}
