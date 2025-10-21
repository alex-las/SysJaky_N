using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Schema;
using SysJaky_N.Models.Billing;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaXmlBuilder
{
    private readonly IReadOnlyCollection<XmlSchema> _schemas;

    public PohodaXmlBuilder(IEnumerable<XmlSchema> schemas)
    {
        if (schemas is null)
        {
            throw new ArgumentNullException(nameof(schemas));
        }

        _schemas = schemas as IReadOnlyCollection<XmlSchema>
            ?? schemas.ToList().AsReadOnly();
    }

    public string BuildIssuedInvoiceXml(Invoice invoice, string? applicationName = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var xml = PohodaOrderPayload.CreateInvoiceDataPack(invoice, applicationName);
        PohodaOrderPayload.ValidateAgainstXsd(xml, _schemas);
        return xml;
    }

}
