using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models.Billing;

public sealed record Invoice(
    [property: Required, StringLength(128)] string ExternalId,
    [property: Required] InvoiceHeader Header,
    [property: Required, MinLength(0)] IReadOnlyList<InvoiceItem> Items,
    [property: Required] VatSummary Summary
);
