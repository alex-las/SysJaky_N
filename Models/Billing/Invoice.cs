using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SysJaky_N.Models.Billing;

public sealed record Invoice(
    [property: Required] InvoiceHeader Header,
    [property: Required] IReadOnlyList<InvoiceItem> Items,
    [property: Required] VatSummary Summary)
{
    public static Invoice Create(InvoiceHeader header, IEnumerable<InvoiceItem> items, VatSummary summary)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(summary);

        var materializedItems = items as IList<InvoiceItem> ?? items.ToList();
        IReadOnlyList<InvoiceItem> readOnlyItems = materializedItems as IReadOnlyList<InvoiceItem>
            ?? new ReadOnlyCollection<InvoiceItem>(materializedItems.ToList());

        return new Invoice(header, readOnlyItems, summary);
    }
}
