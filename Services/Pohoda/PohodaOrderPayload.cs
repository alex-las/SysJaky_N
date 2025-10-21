using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SysJaky_N.Models.Billing;

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

    public static string CreateInvoiceDataPack(Invoice invoice, string? applicationName = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ValidateInvoice(invoice);

        var header = BuildInvoiceHeader(invoice.Header);
        var detail = BuildInvoiceDetail(invoice.Items);
        var summary = BuildInvoiceSummary(invoice.Summary);

        var dataPack = new XElement(Dat + "dataPack",
            new XAttribute(XNamespace.Xmlns + "dat", Dat),
            new XAttribute(XNamespace.Xmlns + "inv", Inv),
            new XAttribute(XNamespace.Xmlns + "typ", Typ),
            new XAttribute("id", $"Order-{invoice.ExternalId}"),
            new XAttribute("version", "2.0"));

        if (!string.IsNullOrWhiteSpace(applicationName))
        {
            dataPack.Add(new XAttribute("application", applicationName));
        }

        dataPack.Add(
            new XElement(Dat + "dataPackItem",
                new XAttribute("id", $"Invoice-{invoice.ExternalId}"),
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
                    new XElement(Lst + "invoiceType", "issuedInvoice"),
                    new XElement(Lst + "filter",
                        new XElement(Ftr + "number", externalId)))));

        var document = new XDocument(new XDeclaration("1.0", "windows-1250", null), dataPack);
        return WriteDocument(document);
    }

    private static XElement BuildInvoiceHeader(InvoiceHeader header)
    {
        var headerElement = new XElement(Inv + "invoiceHeader",
            new XElement(Inv + "invoiceType", MapInvoiceType(header.Type)),
            new XElement(Inv + "numberOrder", header.OrderNumber),
            new XElement(Inv + "text", header.Text),
            new XElement(Inv + "date", header.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(Inv + "dateTax", header.TaxDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(Inv + "dateDue", header.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(Inv + "symVar", header.VariableSymbol));

        if (!string.IsNullOrWhiteSpace(header.SpecificSymbol))
        {
            headerElement.Add(new XElement(Inv + "symSpec", header.SpecificSymbol));
        }

        if (header.Customer is not null)
        {
            var partnerIdentity = BuildPartnerIdentity(header.Customer);
            if (partnerIdentity is not null)
            {
                headerElement.Add(partnerIdentity);
            }
        }

        if (!string.IsNullOrWhiteSpace(header.Note))
        {
            headerElement.Add(new XElement(Inv + "note", header.Note));
        }

        return headerElement;
    }

    private static XElement? BuildPartnerIdentity(CustomerIdentity customer)
    {
        var address = new XElement(Typ + "address");
        if (!string.IsNullOrWhiteSpace(customer.Company))
        {
            address.Add(new XElement(Typ + "company", customer.Company));
        }

        if (!string.IsNullOrWhiteSpace(customer.ContactName))
        {
            address.Add(new XElement(Typ + "name", customer.ContactName));
        }

        if (!string.IsNullOrWhiteSpace(customer.Street))
        {
            address.Add(new XElement(Typ + "street", customer.Street));
        }

        if (!string.IsNullOrWhiteSpace(customer.City))
        {
            address.Add(new XElement(Typ + "city", customer.City));
        }

        if (!string.IsNullOrWhiteSpace(customer.PostalCode))
        {
            address.Add(new XElement(Typ + "zip", customer.PostalCode));
        }

        if (!string.IsNullOrWhiteSpace(customer.Country))
        {
            address.Add(new XElement(Typ + "country", customer.Country));
        }

        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            address.Add(new XElement(Typ + "email", customer.Email));
        }

        if (!string.IsNullOrWhiteSpace(customer.Phone))
        {
            address.Add(new XElement(Typ + "phone", customer.Phone));
        }

        if (!string.IsNullOrWhiteSpace(customer.IdentificationNumber) || !string.IsNullOrWhiteSpace(customer.TaxNumber))
        {
            var identity = new XElement(Typ + "identity",
                string.IsNullOrWhiteSpace(customer.IdentificationNumber) ? null : new XElement(Typ + "ico", customer.IdentificationNumber),
                string.IsNullOrWhiteSpace(customer.TaxNumber) ? null : new XElement(Typ + "dic", customer.TaxNumber));

            return new XElement(Inv + "partnerIdentity", address, identity);
        }

        return address.HasElements
            ? new XElement(Inv + "partnerIdentity", address)
            : null;
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

    private static XElement BuildInvoiceSummary(VatSummary summary)
    {
        var summaryElement = new XElement(Inv + "invoiceSummary",
            new XElement(Inv + "round", "none"),
            new XElement(Inv + "homeCurrency",
                summary.NoneRateExclVat > 0m ? new XElement(Typ + "priceNone", FormatDecimal(summary.NoneRateExclVat)) : null,
                summary.LowRateExclVat > 0m ? new XElement(Typ + "priceLow", FormatDecimal(summary.LowRateExclVat)) : null,
                summary.LowRateVat > 0m ? new XElement(Typ + "priceLowVAT", FormatDecimal(summary.LowRateVat)) : null,
                summary.HighRateExclVat > 0m ? new XElement(Typ + "priceHigh", FormatDecimal(summary.HighRateExclVat)) : null,
                summary.HighRateVat > 0m ? new XElement(Typ + "priceHighVAT", FormatDecimal(summary.HighRateVat)) : null,
                new XElement(Typ + "priceSum", FormatDecimal(summary.TotalInclVat))));

        return summaryElement;
    }

    private static string MapInvoiceType(InvoiceType type) => type switch
    {
        InvoiceType.IssuedInvoice => "issuedInvoice",
        _ => "issuedInvoice"
    };

    private static string MapVatRate(VatRate rate) => rate switch
    {
        VatRate.High => "high",
        VatRate.Low => "low",
        _ => "none"
    };

    private static string FormatDecimal(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string WriteDocument(XDocument document)
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

    private static void ValidateInvoice(Invoice invoice)
    {
        var context = new ValidationContext(invoice);
        Validator.ValidateObject(invoice, context, validateAllProperties: true);

        if (invoice.Items is not null)
        {
            foreach (var item in invoice.Items)
            {
                Validator.ValidateObject(item, new ValidationContext(item), validateAllProperties: true);
            }
        }

        Validator.ValidateObject(invoice.Header, new ValidationContext(invoice.Header), validateAllProperties: true);
        Validator.ValidateObject(invoice.Summary, new ValidationContext(invoice.Summary), validateAllProperties: true);
    }
}
