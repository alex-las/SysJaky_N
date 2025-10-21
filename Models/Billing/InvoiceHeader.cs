using System;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models.Billing;

public sealed record InvoiceHeader(
    [property: Required]
    InvoiceType Type,
    [property: Required]
    DateOnly IssueDate,
    [property: Required]
    DateOnly TaxDate,
    [property: Required]
    DateOnly DueDate,
    [property: Required, StringLength(64)]
    string OrderNumber,
    [property: Required, StringLength(256)]
    string Text,
    [property: Required, StringLength(32)]
    string VariableSymbol,
    [property: StringLength(32)]
    string? SpecificSymbol = null,
    [property: StringLength(1024)]
    string? Note = null,
    CustomerIdentity? Customer = null
);
