using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Linq;

namespace SysJaky_N.Services.Pohoda;

public static class PohodaOrderPayload
{
    private static readonly XNamespace Dat = "http://www.stormware.cz/schema/version_2/data.xsd";
    private static readonly XNamespace Inv = "http://www.stormware.cz/schema/version_2/invoice.xsd";
    private static readonly XNamespace Typ = "http://www.stormware.cz/schema/version_2/type.xsd";
    private static readonly XNamespace Lst = "http://www.stormware.cz/schema/version_2/list.xsd";
    private static readonly XNamespace Ftr = "http://www.stormware.cz/schema/version_2/filter.xsd";

    private static readonly Encoding Windows1250Encoding;

    static PohodaOrderPayload()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1250Encoding = Encoding.GetEncoding("windows-1250");
    }

    public static string CreateInvoiceDataPack(InvoiceDto invoice, string? applicationName = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var header = BuildInvoiceHeader(invoice.Header);
        var detail = BuildInvoiceDetail(invoice.Items);
        var summary = BuildInvoiceSummary(invoice);

        var dataPack = new XElement(Dat + "dataPack",
            new XAttribute(XNamespace.Xmlns + "dat", Dat),
            new XAttribute(XNamespace.Xmlns + "inv", Inv),
            new XAttribute(XNamespace.Xmlns + "typ", Typ),
            new XAttribute("id", $"Invoice-{invoice.Header.OrderNumber}"),
            new XAttribute("version", "2.0"));

        if (!string.IsNullOrWhiteSpace(applicationName))
        {
            dataPack.Add(new XAttribute("application", applicationName));
        }

        dataPack.Add(
                new XElement(Dat + "dataPackItem",
                new XAttribute("id", $"Invoice-{invoice.Header.OrderNumber}"),
                new XAttribute("version", "2.0"),
                new XElement(Inv + "invoice",
                    header,
                    detail,
                    summary)));

        var document = new XDocument(new XDeclaration("1.0", "windows-1250", null), dataPack);
        return WriteDocument(document);
    }

    public static string CreateListInvoiceRequest(string externalId, string? applicationName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(externalId);

        var dataPack = new XElement(Dat + "dataPack",
            new XAttribute(XNamespace.Xmlns + "dat", Dat),
            new XAttribute(XNamespace.Xmlns + "inv", Inv),
            new XAttribute(XNamespace.Xmlns + "typ", Typ),
            new XAttribute(XNamespace.Xmlns + "lst", Lst),
            new XAttribute(XNamespace.Xmlns + "ftr", Ftr),
            new XAttribute("id", $"ListInvoice-{externalId}"),
            new XAttribute("version", "2.0"));

        if (!string.IsNullOrWhiteSpace(applicationName))
        {
            dataPack.Add(new XAttribute("application", applicationName));
        }

        dataPack.Add(
            new XElement(Dat + "dataPackItem",
                new XAttribute("id", $"InvoiceList-{externalId}"),
                new XAttribute("version", "2.0"),
                new XElement(Lst + "listInvoiceRequest",
                    new XAttribute("version", "2.0"),
                    new XAttribute("invoiceType", "issuedInvoice"),
                    new XAttribute("invoiceVersion", "2.0"),
                    new XElement(Lst + "requestInvoice"),
                    new XElement(Ftr + "filter",
                        new XElement(Ftr + "number", externalId)))));

        var document = new XDocument(new XDeclaration("1.0", "windows-1250", null), dataPack);
        return WriteDocument(document);
    }

    public static void ValidateAgainstXsd(string xml, IEnumerable<XmlSchema> schemas)
    {
        ArgumentException.ThrowIfNullOrEmpty(xml);

        if (schemas is null)
        {
            throw new ArgumentNullException(nameof(schemas));
        }

        var materialized = schemas as ICollection<XmlSchema> ?? schemas.ToList();
        if (materialized.Count == 0)
        {
            return;
        }

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema
        };

        foreach (var schema in materialized)
        {
            settings.Schemas.Add(schema);
        }

        var errors = new List<string>();
        settings.ValidationEventHandler += (_, args) =>
        {
            if (args.Severity == XmlSeverityType.Error)
            {
                errors.Add(args.Message);
            }
        };

        using var stringReader = new StringReader(xml);
        using var reader = XmlReader.Create(stringReader, settings);
        while (reader.Read())
        {
        }

        if (errors.Count > 0)
        {
            throw new XmlSchemaValidationException($"XML validation failed: {string.Join("; ", errors)}");
        }
    }

    private static XElement BuildInvoiceHeader(InvoiceHeader header)
    {
        var headerElement = new XElement(Inv + "invoiceHeader",
            new XElement(Inv + "invoiceType", header.InvoiceType),
            new XElement(Inv + "numberOrder", header.OrderNumber),
            new XElement(Inv + "text", header.Text),
            new XElement(Inv + "date", header.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(Inv + "dateTax", header.TaxDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(Inv + "dateDue", header.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(Inv + "symVar", header.VariableSymbol));

        if (!string.IsNullOrWhiteSpace(header.SpecificSymbol))
        {
            headerElement.Add(new XElement(Inv + "symSpec", header.SpecificSymbol));
        }

        if (header.Customer is not null)
        {
            headerElement.Add(new XElement(Inv + "partnerIdentity", BuildCustomerAddress(header.Customer)));
        }

        if (!string.IsNullOrWhiteSpace(header.Note))
        {
            headerElement.Add(new XElement(Inv + "note", header.Note));
        }

        return headerElement;
    }

    private static XElement BuildInvoiceDetail(IReadOnlyList<InvoiceItem> items)
    {
        var detail = new XElement(Inv + "invoiceDetail");

        foreach (var item in items)
        {
            var invoiceItem = new XElement(Inv + "invoiceItem",
                new XElement(Inv + "text", item.Name),
                new XElement(Inv + "quantity", item.Quantity.ToString(CultureInfo.InvariantCulture)),
                new XElement(Inv + "rateVAT", MapVatRate(item.Rate)),
                new XElement(Inv + "homeCurrency",
                    new XElement(Typ + "unitPrice", FormatDecimal(item.UnitPriceExclVat)),
                    new XElement(Typ + "price", FormatDecimal(item.TotalExclVat)),
                    new XElement(Typ + "priceVAT", FormatDecimal(item.VatAmount)),
                    new XElement(Typ + "priceSum", FormatDecimal(item.TotalInclVat))));

            if (item.Discount > 0m)
            {
                invoiceItem.Add(new XElement(Inv + "discountPercentage", FormatDecimal(item.DiscountPercentage)));
            }

            detail.Add(invoiceItem);
        }

        return detail;
    }

    private static XElement BuildInvoiceSummary(InvoiceDto invoice)
    {
        var summaryElement = new XElement(Inv + "invoiceSummary",
            new XElement(Inv + "round", "none"),
            new XElement(Inv + "homeCurrency",
                invoice.NoneRateBase is > 0m ? new XElement(Typ + "priceNone", FormatDecimal(invoice.NoneRateBase.Value)) : null,
                invoice.LowRateBase is > 0m ? new XElement(Typ + "priceLow", FormatDecimal(invoice.LowRateBase.Value)) : null,
                invoice.LowRateVat is > 0m ? new XElement(Typ + "priceLowVAT", FormatDecimal(invoice.LowRateVat.Value)) : null,
                invoice.HighRateBase is > 0m ? new XElement(Typ + "priceHigh", FormatDecimal(invoice.HighRateBase.Value)) : null,
                invoice.HighRateVat is > 0m ? new XElement(Typ + "priceHighVAT", FormatDecimal(invoice.HighRateVat.Value)) : null,
                new XElement(Typ + "priceSum", FormatDecimal(invoice.TotalInclVat))));

        return summaryElement;
    }

    private static XElement BuildCustomerAddress(CustomerIdentity identity)
    {
        var address = new XElement(Typ + "address");

        if (!string.IsNullOrWhiteSpace(identity.Company))
        {
            address.Add(new XElement(Typ + "company", identity.Company));
        }

        if (!string.IsNullOrWhiteSpace(identity.Name))
        {
            address.Add(new XElement(Typ + "name", identity.Name));
        }

        if (!string.IsNullOrWhiteSpace(identity.Street))
        {
            address.Add(new XElement(Typ + "street", identity.Street));
        }

        if (!string.IsNullOrWhiteSpace(identity.City))
        {
            address.Add(new XElement(Typ + "city", identity.City));
        }

        if (!string.IsNullOrWhiteSpace(identity.Zip))
        {
            address.Add(new XElement(Typ + "zip", identity.Zip));
        }

        if (!string.IsNullOrWhiteSpace(identity.Country))
        {
            address.Add(new XElement(Typ + "country", identity.Country));
        }

        return address;
    }

    private static string MapVatRate(VatRate rate) => rate switch
    {
        VatRate.High => "high",
        VatRate.Low => "low",
        _ => "none"
    };

    private static string FormatDecimal(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    internal static string WriteDocument(XDocument document)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = Windows1250Encoding,
            Indent = true,
            OmitXmlDeclaration = false,
            NewLineHandling = NewLineHandling.Replace
        };

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            document.WriteTo(writer);
        }

        return Windows1250Encoding.GetString(stream.ToArray());
    }
}
