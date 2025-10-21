using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Schema;
using SysJaky_N.Models.Billing;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaXmlBuilder
{
    private readonly PohodaXmlOptions _options;
    private readonly IReadOnlyCollection<XmlSchema> _schemas;

    public PohodaXmlBuilder(PohodaXmlOptions options)
        : this(options, PohodaXmlSchemas.Default)
    {
    }

    public PohodaXmlBuilder(PohodaXmlOptions options, IEnumerable<XmlSchema> schemas)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentNullException.ThrowIfNull(schemas);

        _schemas = schemas as IReadOnlyCollection<XmlSchema> ?? schemas.ToArray();

        if (_schemas.Count == 0)
        {
            throw new ArgumentException("At least one schema must be provided.", nameof(schemas));
        }
    }

    public string BuildIssuedInvoiceXml(Invoice invoice)
    {
        var document = PohodaOrderPayload.CreateInvoiceDataPackDocument(invoice, _options.Application);
        Validate(document);
        return PohodaOrderPayload.WriteDocument(document);
    }

    public string BuildListInvoiceRequest(string externalId)
    {
        var document = PohodaOrderPayload.CreateListInvoiceRequestDocument(externalId, _options.Application);
        Validate(document);
        return PohodaOrderPayload.WriteDocument(document);
    }

    private void Validate(XDocument document)
    {
        PohodaOrderPayload.ValidateAgainstXsd(document, _schemas);
    }
}
