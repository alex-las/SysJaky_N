using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaXmlBuilder
{
    private readonly IReadOnlyCollection<XmlSchema> _schemas;
    private static readonly Encoding Windows1250Encoding;

    static PohodaXmlBuilder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1250Encoding = Encoding.GetEncoding("windows-1250");
    }

    public PohodaXmlBuilder(IEnumerable<XmlSchema> schemas)
    {
        if (schemas is null)
        {
            throw new ArgumentNullException(nameof(schemas));
        }

        _schemas = schemas as IReadOnlyCollection<XmlSchema>
            ?? schemas.ToList().AsReadOnly();
    }

    public string BuildIssuedInvoiceXml(InvoiceDto invoice, string? applicationName = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var settings = new XmlWriterSettings
        {
            Encoding = Windows1250Encoding,
            Indent = true,
            OmitXmlDeclaration = false,
            NewLineHandling = NewLineHandling.Entitize
        };

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            WriteInvoice(writer, invoice, applicationName);
        }

        var xml = Windows1250Encoding.GetString(stream.ToArray());
        ValidateAgainstSchemas(xml);
        return xml;
    }

    private static void WriteInvoice(XmlWriter writer, InvoiceDto invoice, string? applicationName)
    {
        const string DatNamespace = "http://www.stormware.cz/schema/version_2/data.xsd";
        const string InvNamespace = "http://www.stormware.cz/schema/version_2/invoice.xsd";
        const string TypNamespace = "http://www.stormware.cz/schema/version_2/type.xsd";

        writer.WriteStartDocument();
        writer.WriteStartElement("dat", "dataPack", DatNamespace);
        writer.WriteAttributeString("xmlns", "dat", null, DatNamespace);
        writer.WriteAttributeString("xmlns", "inv", null, InvNamespace);
        writer.WriteAttributeString("xmlns", "typ", null, TypNamespace);

        var identifier = $"Invoice-{invoice.Header.OrderNumber}";
        writer.WriteAttributeString("id", identifier);
        writer.WriteAttributeString("version", "2.0");
        if (!string.IsNullOrWhiteSpace(applicationName))
        {
            writer.WriteAttributeString("application", applicationName);
        }

        writer.WriteStartElement("dat", "dataPackItem", DatNamespace);
        writer.WriteAttributeString("id", identifier);
        writer.WriteAttributeString("version", "2.0");

        writer.WriteStartElement("inv", "invoice", InvNamespace);
        WriteInvoiceHeader(writer, invoice.Header, InvNamespace, TypNamespace);
        WriteInvoiceDetail(writer, invoice.Items, InvNamespace, TypNamespace);
        WriteInvoiceSummary(writer, invoice, InvNamespace, TypNamespace);
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteInvoiceHeader(XmlWriter writer, InvoiceHeader header, string invNamespace, string typNamespace)
    {
        writer.WriteStartElement("inv", "invoiceHeader", invNamespace);
        writer.WriteElementString("inv", "invoiceType", invNamespace, header.InvoiceType);
        writer.WriteElementString("inv", "numberOrder", invNamespace, header.OrderNumber);
        writer.WriteElementString("inv", "text", invNamespace, header.Text);
        writer.WriteElementString("inv", "date", invNamespace, header.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        writer.WriteElementString("inv", "dateTax", invNamespace, header.TaxDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        writer.WriteElementString("inv", "dateDue", invNamespace, header.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        writer.WriteElementString("inv", "symVar", invNamespace, header.VariableSymbol);

        if (!string.IsNullOrWhiteSpace(header.SpecificSymbol))
        {
            writer.WriteElementString("inv", "symSpec", invNamespace, header.SpecificSymbol);
        }

        if (header.Customer is not null)
        {
            writer.WriteStartElement("inv", "partnerIdentity", invNamespace);
            writer.WriteStartElement("typ", "address", typNamespace);
            WriteCustomerField(writer, typNamespace, "company", header.Customer.Company);
            WriteCustomerField(writer, typNamespace, "name", header.Customer.Name);
            WriteCustomerField(writer, typNamespace, "street", header.Customer.Street);
            WriteCustomerField(writer, typNamespace, "city", header.Customer.City);
            WriteCustomerField(writer, typNamespace, "zip", header.Customer.Zip);
            WriteCustomerField(writer, typNamespace, "country", header.Customer.Country);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        if (!string.IsNullOrWhiteSpace(header.Note))
        {
            writer.WriteElementString("inv", "note", invNamespace, header.Note);
        }

        writer.WriteEndElement();
    }

    private static void WriteCustomerField(XmlWriter writer, string typNamespace, string elementName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            writer.WriteElementString("typ", elementName, typNamespace, value);
        }
    }

    private static void WriteInvoiceDetail(XmlWriter writer, IReadOnlyCollection<InvoiceItem> items, string invNamespace, string typNamespace)
    {
        writer.WriteStartElement("inv", "invoiceDetail", invNamespace);

        foreach (var item in items)
        {
            writer.WriteStartElement("inv", "invoiceItem", invNamespace);
            writer.WriteElementString("inv", "text", invNamespace, item.Name);
            writer.WriteElementString("inv", "quantity", invNamespace, item.Quantity.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("inv", "rateVAT", invNamespace, MapVatRate(item.Rate));

            writer.WriteStartElement("inv", "homeCurrency", invNamespace);
            writer.WriteElementString("typ", "unitPrice", typNamespace, FormatDecimal(item.UnitPriceExclVat));
            writer.WriteEndElement();

            if (item.Discount > 0m)
            {
                writer.WriteElementString("inv", "discountPercentage", invNamespace, FormatDecimal(item.DiscountPercentage));
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteInvoiceSummary(XmlWriter writer, InvoiceDto invoice, string invNamespace, string typNamespace)
    {
        writer.WriteStartElement("inv", "invoiceSummary", invNamespace);
        writer.WriteElementString("inv", "round", invNamespace, "none");
        writer.WriteStartElement("inv", "homeCurrency", invNamespace);

        WriteOptionalSummaryElement(writer, typNamespace, "priceNone", invoice.NoneRateBase);
        WriteOptionalSummaryElement(writer, typNamespace, "priceLow", invoice.LowRateBase);
        WriteOptionalSummaryElement(writer, typNamespace, "priceLowVAT", invoice.LowRateVat);
        WriteOptionalSummaryElement(writer, typNamespace, "priceHigh", invoice.HighRateBase);
        WriteOptionalSummaryElement(writer, typNamespace, "priceHighVAT", invoice.HighRateVat);
        writer.WriteElementString("typ", "priceSum", typNamespace, FormatDecimal(invoice.TotalInclVat));

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteOptionalSummaryElement(XmlWriter writer, string typNamespace, string elementName, decimal? value)
    {
        if (value is > 0m)
        {
            writer.WriteElementString("typ", elementName, typNamespace, FormatDecimal(value.Value));
        }
    }

    private static string MapVatRate(VatRate rate) => rate switch
    {
        VatRate.High => "high",
        VatRate.Low => "low",
        _ => "none"
    };

    private static string FormatDecimal(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private void ValidateAgainstSchemas(string xml)
    {
        if (_schemas.Count == 0)
        {
            return;
        }

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema
        };

        foreach (var schema in _schemas)
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
}
