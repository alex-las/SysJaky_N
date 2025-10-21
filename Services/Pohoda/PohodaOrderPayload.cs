using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using SysJaky_N.Models;

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

    public static string CreateInvoiceDataPack(Order order, string? applicationName = null)
    {
        ArgumentNullException.ThrowIfNull(order);

        var items = MapOrderItems(order);

        var header = BuildInvoiceHeader(order);
        var detail = BuildInvoiceDetail(items);
        var summary = BuildInvoiceSummary(order, items);

        var dataPack = new XElement(Dat + "dataPack",
            new XAttribute(XNamespace.Xmlns + "dat", Dat),
            new XAttribute(XNamespace.Xmlns + "inv", Inv),
            new XAttribute(XNamespace.Xmlns + "typ", Typ),
            new XAttribute("id", $"Order-{order.Id}"),
            new XAttribute("version", "2.0"));

        if (!string.IsNullOrWhiteSpace(applicationName))
        {
            dataPack.Add(new XAttribute("application", applicationName));
        }

        dataPack.Add(
            new XElement(Dat + "dataPackItem",
                new XAttribute("id", $"Invoice-{order.Id}"),
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

    private static XElement BuildInvoiceHeader(Order order)
    {
        var header = new XElement(Inv + "invoiceHeader",
            new XElement(Inv + "invoiceType", "issuedInvoice"),
            new XElement(Inv + "numberOrder", order.Id.ToString(CultureInfo.InvariantCulture)),
            new XElement(Inv + "text", $"Objednávka {order.Id}"),
            new XElement(Inv + "date", order.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(Inv + "dateTax", order.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(Inv + "dateDue", order.CreatedAt.AddDays(14).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(Inv + "symVar", order.Id.ToString(CultureInfo.InvariantCulture)));

        if (!string.IsNullOrWhiteSpace(order.PaymentConfirmation))
        {
            header.Add(new XElement(Inv + "symSpec", order.PaymentConfirmation));
        }

        if (!string.IsNullOrWhiteSpace(order.UserId))
        {
            header.Add(new XElement(Inv + "partnerIdentity",
                new XElement(Typ + "address",
                    new XElement(Typ + "company", order.UserId))));
        }

        if (!string.IsNullOrWhiteSpace(order.InvoicePath))
        {
            header.Add(new XElement(Inv + "note", order.InvoicePath));
        }

        return header;
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

    private static XElement BuildInvoiceSummary(Order order, IReadOnlyList<InvoiceItem> items)
    {
        var total = items.Aggregate(new SummaryTotals(), (acc, item) => acc with
        {
            TotalExclVat = acc.TotalExclVat + item.TotalExclVat,
            TotalVat = acc.TotalVat + item.VatAmount,
            HighRateExclVat = acc.HighRateExclVat + (item.Rate == VatRate.High ? item.TotalExclVat : 0m),
            HighRateVat = acc.HighRateVat + (item.Rate == VatRate.High ? item.VatAmount : 0m),
            LowRateExclVat = acc.LowRateExclVat + (item.Rate == VatRate.Low ? item.TotalExclVat : 0m),
            LowRateVat = acc.LowRateVat + (item.Rate == VatRate.Low ? item.VatAmount : 0m),
            NoneRateExclVat = acc.NoneRateExclVat + (item.Rate == VatRate.None ? item.TotalExclVat : 0m)
        });

        var summary = new XElement(Inv + "invoiceSummary",
            new XElement(Inv + "round", "none"),
            new XElement(Inv + "homeCurrency",
                total.NoneRateExclVat > 0m ? new XElement(Typ + "priceNone", FormatDecimal(total.NoneRateExclVat)) : null,
                total.LowRateExclVat > 0m ? new XElement(Typ + "priceLow", FormatDecimal(total.LowRateExclVat)) : null,
                total.LowRateVat > 0m ? new XElement(Typ + "priceLowVAT", FormatDecimal(total.LowRateVat)) : null,
                total.HighRateExclVat > 0m ? new XElement(Typ + "priceHigh", FormatDecimal(total.HighRateExclVat)) : null,
                total.HighRateVat > 0m ? new XElement(Typ + "priceHighVAT", FormatDecimal(total.HighRateVat)) : null,
                new XElement(Typ + "priceSum", FormatDecimal(order.Total))));

        return summary;
    }

    private static IReadOnlyList<InvoiceItem> MapOrderItems(Order order)
    {
        var items = order.Items ?? new List<OrderItem>();
        if (items.Count == 0)
        {
            return Array.Empty<InvoiceItem>();
        }

        var discountTotal = Math.Max(order.TotalPrice - order.Total, 0m);
        var roundedItems = items
            .Select(item => new
            {
                Item = item,
                Total = RoundCurrency(item.Total),
                Vat = RoundCurrency(item.Vat)
            })
            .ToList();

        var grossSum = roundedItems.Sum(i => i.Total);
        var mapped = new List<InvoiceItem>(roundedItems.Count);
        decimal allocatedDiscount = 0m;

        for (var i = 0; i < roundedItems.Count; i++)
        {
            var current = roundedItems[i];
            var item = current.Item;
            var totalInclVat = current.Total;
            var vatAmount = current.Vat;
            var totalExclVat = RoundCurrency(totalInclVat - vatAmount);
            var rate = DetermineVatRate(totalExclVat, vatAmount);

            var itemDiscount = 0m;
            if (discountTotal > 0m && grossSum > 0m)
            {
                if (i == roundedItems.Count - 1)
                {
                    itemDiscount = RoundCurrency(discountTotal - allocatedDiscount);
                }
                else
                {
                    var ratio = totalInclVat / grossSum;
                    itemDiscount = RoundCurrency(discountTotal * ratio);
                    allocatedDiscount += itemDiscount;
                }
            }

            var quantity = Math.Max(1, item.Quantity);
            var unitPriceExclVat = quantity == 0
                ? totalExclVat
                : RoundCurrency(item.UnitPriceExclVat);

            var invoiceItem = new InvoiceItem(
                Name: item.Course?.Title ?? $"Course #{item.CourseId}",
                Quantity: quantity,
                UnitPriceExclVat: unitPriceExclVat,
                TotalExclVat: totalExclVat,
                VatAmount: vatAmount,
                TotalInclVat: totalInclVat,
                Discount: itemDiscount,
                Rate: rate);

            mapped.Add(invoiceItem);
        }

        if (discountTotal > 0m && mapped.Count > 0)
        {
            var sum = mapped.Sum(p => p.Discount);
            var delta = RoundCurrency(discountTotal - sum);
            if (delta != 0m)
            {
                var last = mapped[^1];
                mapped[^1] = last with { Discount = RoundCurrency(last.Discount + delta) };
            }
        }

        return mapped;
    }

    private static VatRate DetermineVatRate(decimal baseAmount, decimal vatAmount)
    {
        if (baseAmount == 0m)
        {
            return VatRate.None;
        }

        var rate = vatAmount / baseAmount * 100m;
        if (rate >= 20m)
        {
            return VatRate.High;
        }

        if (rate >= 10m)
        {
            return VatRate.Low;
        }

        return VatRate.None;
    }

    private static string MapVatRate(VatRate rate) => rate switch
    {
        VatRate.High => "high",
        VatRate.Low => "low",
        _ => "none"
    };

    private static string FormatDecimal(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static decimal RoundCurrency(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

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

    private sealed record InvoiceItem(
        string Name,
        int Quantity,
        decimal UnitPriceExclVat,
        decimal TotalExclVat,
        decimal VatAmount,
        decimal TotalInclVat,
        decimal Discount,
        VatRate Rate)
    {
        public decimal DiscountPercentage => TotalInclVat == 0m
            ? 0m
            : RoundCurrency(Discount / TotalInclVat * 100m);
    }

    private sealed record SummaryTotals
    {
        public decimal TotalExclVat { get; init; }
        public decimal TotalVat { get; init; }
        public decimal HighRateExclVat { get; init; }
        public decimal HighRateVat { get; init; }
        public decimal LowRateExclVat { get; init; }
        public decimal LowRateVat { get; init; }
        public decimal NoneRateExclVat { get; init; }
    }

    private enum VatRate
    {
        None,
        Low,
        High
    }
}
