using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaListParser
{
    private static readonly string[] NumberCandidates =
    [
        "number",
        "numberRequested",
        "numberAssigned",
        "invoiceNumber"
    ];

    private static readonly string[] DueDateCandidates =
    [
        "dateDue",
        "dueDate"
    ];

    private static readonly string[] PaidDateCandidates =
    [
        "dateOfPayment",
        "datePayment",
        "datePaid",
        "datePay"
    ];

    private static readonly string[] PaidValueCandidates =
    [
        "paid",
        "isPaid",
        "paymentState"
    ];

    public IReadOnlyCollection<InvoiceStatus> Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return Array.Empty<InvoiceStatus>();
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (Exception ex) when (ex is XmlException or FormatException)
        {
            throw new PohodaXmlException("Failed to parse Pohoda list invoice response.", xml);
        }

        var invoices = document
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "invoice", StringComparison.OrdinalIgnoreCase))
            .Select(ParseInvoice)
            .Where(status => status is not null)
            .Select(status => status!)
            .ToArray();

        return invoices;
    }

    private static InvoiceStatus? ParseInvoice(XElement invoiceElement)
    {
        var number = ExtractNumber(invoiceElement);
        var symVar = ExtractValue(invoiceElement, "symVar");
        var total = ExtractDecimal(invoiceElement, "priceSum") ?? 0m;
        var dueDate = ExtractDate(invoiceElement, DueDateCandidates);
        var paidAt = ExtractDate(invoiceElement, PaidDateCandidates);
        var paid = ExtractBoolean(invoiceElement, PaidValueCandidates) ?? paidAt is not null;

        if (number is null && symVar is null && total == 0m && dueDate is null && paidAt is null)
        {
            return null;
        }

        return new InvoiceStatus(number, symVar, total, paid, dueDate, paidAt);
    }

    private static string? ExtractNumber(XElement invoiceElement)
    {
        foreach (var candidate in NumberCandidates)
        {
            var element = invoiceElement
                .Descendants()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, candidate, StringComparison.OrdinalIgnoreCase));

            if (element is null)
            {
                continue;
            }

            if (!element.HasElements)
            {
                var text = element.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            foreach (var descendant in element.DescendantsAndSelf())
            {
                var value = descendant.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static string? ExtractValue(XElement invoiceElement, string localName)
    {
        var element = invoiceElement
            .Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

        var value = element?.Value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static decimal? ExtractDecimal(XElement invoiceElement, string localName)
    {
        var value = ExtractValue(invoiceElement, localName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static DateOnly? ExtractDate(XElement invoiceElement, IEnumerable<string> localNames)
    {
        foreach (var name in localNames)
        {
            var value = ExtractValue(invoiceElement, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedDateTime))
            {
                return DateOnly.FromDateTime(parsedDateTime);
            }

            if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateOnly))
            {
                return parsedDateOnly;
            }
        }

        return null;
    }

    private static bool? ExtractBoolean(XElement invoiceElement, IEnumerable<string> localNames)
    {
        foreach (var name in localNames)
        {
            var value = ExtractValue(invoiceElement, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (bool.TryParse(value, out var parsedBool))
            {
                return parsedBool;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
            {
                if (parsedInt == 1)
                {
                    return true;
                }

                if (parsedInt == 0)
                {
                    return false;
                }
            }
        }

        return null;
    }
}
