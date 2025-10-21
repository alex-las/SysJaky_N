using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Schema;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaListRequestBuilder
{
    private static readonly XNamespace Dat = "http://www.stormware.cz/schema/version_2/data.xsd";
    private static readonly XNamespace Inv = "http://www.stormware.cz/schema/version_2/invoice.xsd";
    private static readonly XNamespace Typ = "http://www.stormware.cz/schema/version_2/type.xsd";
    private static readonly XNamespace Lst = "http://www.stormware.cz/schema/version_2/list.xsd";
    private static readonly XNamespace Ftr = "http://www.stormware.cz/schema/version_2/filter.xsd";

    private readonly IReadOnlyCollection<XmlSchema> _schemas;

    public PohodaListRequestBuilder(IEnumerable<XmlSchema> schemas)
    {
        if (schemas is null)
        {
            throw new ArgumentNullException(nameof(schemas));
        }

        _schemas = schemas as IReadOnlyCollection<XmlSchema> ?? schemas.ToArray();
    }

    public string Build(PohodaListFilter filter, string? requestId = null, string? applicationName = null)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var identifier = CreateIdentifier(requestId, filter);
        var dataPack = new XElement(Dat + "dataPack",
            new XAttribute(XNamespace.Xmlns + "dat", Dat),
            new XAttribute(XNamespace.Xmlns + "inv", Inv),
            new XAttribute(XNamespace.Xmlns + "typ", Typ),
            new XAttribute(XNamespace.Xmlns + "lst", Lst),
            new XAttribute(XNamespace.Xmlns + "ftr", Ftr),
            new XAttribute("id", $"ListInvoice-{identifier}"),
            new XAttribute("version", "2.0"));

        if (!string.IsNullOrWhiteSpace(applicationName))
        {
            dataPack.Add(new XAttribute("application", applicationName));
        }

        var request = new XElement(Lst + "listInvoiceRequest",
            new XAttribute("version", "2.0"),
            new XAttribute("invoiceType", "issuedInvoice"),
            new XAttribute("invoiceVersion", "2.0"),
            new XElement(Lst + "requestInvoice"));

        var filterElement = BuildFilter(filter);
        if (filterElement.HasElements)
        {
            request.Add(filterElement);
        }

        var dataPackItem = new XElement(Dat + "dataPackItem",
            new XAttribute("id", $"InvoiceList-{identifier}"),
            new XAttribute("version", "2.0"),
            request);

        dataPack.Add(dataPackItem);

        var document = new XDocument(new XDeclaration("1.0", "windows-1250", null), dataPack);
        var xml = PohodaOrderPayload.WriteDocument(document);
        PohodaOrderPayload.ValidateAgainstXsd(xml, _schemas);
        return xml;
    }

    public XElement BuildFilter(PohodaListFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var filterElement = new XElement(Ftr + "filter");

        foreach (var element in BuildFilterElements(filter))
        {
            filterElement.Add(element);
        }

        return filterElement;
    }

    private static IEnumerable<XElement> BuildFilterElements(PohodaListFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Number))
        {
            yield return new XElement(Ftr + "number", filter.Number);
        }

        if (filter.DateFrom is { } dateFrom)
        {
            yield return new XElement(Ftr + "dateFrom", dateFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (filter.DateTo is { } dateTo)
        {
            yield return new XElement(Ftr + "dateTill", dateTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(filter.VariableSymbol))
        {
            yield return new XElement(Ftr + "symVar", filter.VariableSymbol);
        }
    }

    private static string CreateIdentifier(string? requestId, PohodaListFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            return Sanitize(requestId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Number))
        {
            return Sanitize(filter.Number);
        }

        if (!string.IsNullOrWhiteSpace(filter.VariableSymbol))
        {
            return Sanitize(filter.VariableSymbol);
        }

        if (filter.DateFrom is { } from && filter.DateTo is { } to)
        {
            return $"{from:yyyyMMdd}-{to:yyyyMMdd}";
        }

        if (filter.DateFrom is { } onlyFrom)
        {
            return $"from-{onlyFrom:yyyyMMdd}";
        }

        if (filter.DateTo is { } onlyTo)
        {
            return $"to-{onlyTo:yyyyMMdd}";
        }

        return "All";
    }

    private static string Sanitize(string value)
    {
        var sanitized = new string(value
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "Value" : sanitized;
    }
}
