using System;

namespace SysJaky_N.Services.Pohoda;

public sealed record InvoiceStatus(
    string? Number,
    string? SymVar,
    decimal Total,
    bool Paid,
    DateOnly? DueDate,
    DateOnly? PaidAt);
