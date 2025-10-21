using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using SysJaky_N.Models;
using SysJaky_N.Services.Pohoda;
using Xunit;

namespace SysJaky_N.Tests;

public class PohodaOrderPayloadTests
{
    private static readonly XNamespace Dat = "http://www.stormware.cz/schema/version_2/data.xsd";
    private static readonly XNamespace Inv = "http://www.stormware.cz/schema/version_2/invoice.xsd";
    private static readonly XNamespace Typ = "http://www.stormware.cz/schema/version_2/type.xsd";
    private static readonly XNamespace Lst = "http://www.stormware.cz/schema/version_2/list.xsd";
    private static readonly XNamespace Ftr = "http://www.stormware.cz/schema/version_2/filter.xsd";

    [Fact]
    public void CreateInvoiceDataPack_BuildsInvoiceWithDetailAndSummary()
    {
        var order = CreateSampleOrder();

        var mappedInvoice = OrderToInvoiceMapper.Map(order);
        var xml = PohodaOrderPayload.CreateInvoiceDataPack(mappedInvoice, "SysJaky_N");

        Assert.Contains("encoding=\"windows-1250\"", xml, StringComparison.OrdinalIgnoreCase);

        var document = XDocument.Parse(xml);
        var root = document.Root;
        Assert.NotNull(root);
        Assert.Equal("dataPack", root!.Name.LocalName);
        Assert.Equal("2.0", root.Attribute("version")?.Value);

        var invoiceElement = root.Element(Dat + "dataPackItem")?.Element(Inv + "invoice");
        Assert.NotNull(invoiceElement);

        var header = invoiceElement!.Element(Inv + "invoiceHeader");
        Assert.NotNull(header);
        Assert.Equal($"Objednávka {order.Id}", header!.Element(Inv + "text")?.Value);
        Assert.Equal(order.PaymentConfirmation, header.Element(Inv + "symSpec")?.Value);
        Assert.Equal(order.InvoicePath, header.Element(Inv + "note")?.Value);

        var detail = invoiceElement.Element(Inv + "invoiceDetail");
        Assert.NotNull(detail);
        var items = detail!.Elements(Inv + "invoiceItem").ToList();
        Assert.Equal(order.Items.Count, items.Count);

        var firstItem = items.First();
        Assert.Equal(order.Items[0].Course!.Title, firstItem.Element(Inv + "text")?.Value);
        Assert.Equal(order.Items[0].Quantity.ToString(CultureInfo.InvariantCulture), firstItem.Element(Inv + "quantity")?.Value);

        var currency = firstItem.Element(Inv + "homeCurrency");
        Assert.Equal(order.Items[0].UnitPriceExclVat.ToString("0.##", CultureInfo.InvariantCulture), currency?.Element(Typ + "unitPrice")?.Value);

        var summary = invoiceElement.Element(Inv + "invoiceSummary");
        Assert.NotNull(summary);
        Assert.Equal(order.Total.ToString("0.##", CultureInfo.InvariantCulture), summary!.Descendants(Typ + "priceSum").Single().Value);
    }

    [Fact]
    public void CreateInvoiceDataPack_DistributesDiscount()
    {
        var order = CreateSampleOrder();
        order.TotalPrice = 400m;
        order.Total = 363m;

        var mappedInvoice = OrderToInvoiceMapper.Map(order);
        var xml = PohodaOrderPayload.CreateInvoiceDataPack(mappedInvoice, "SysJaky_N");
        var document = XDocument.Parse(xml);
        var detail = document.Root!
            .Element(Dat + "dataPackItem")!
            .Element(Inv + "invoice")!
            .Element(Inv + "invoiceDetail");

        var discountElements = detail!
            .Elements(Inv + "invoiceItem")
            .Select(item => item.Element(Inv + "discountPercentage")?.Value)
            .Where(value => value is not null)
            .Select(value => decimal.Parse(value!, CultureInfo.InvariantCulture))
            .ToList();

        Assert.NotEmpty(discountElements);
        Assert.True(discountElements.Sum() > 0m);
    }

    [Fact]
    public void CreateListInvoiceRequest_BuildsFilterWithNumber()
    {
        var xml = PohodaOrderPayload.CreateListInvoiceRequest("INV-123", "SysJaky_N");
        var document = XDocument.Parse(xml);

        var filterValue = document.Root!
            .Element(Dat + "dataPackItem")!
            .Element(Lst + "listInvoiceRequest")!
            .Element(Ftr + "filter")!
            .Element(Ftr + "number")!
            .Value;

        Assert.Equal("INV-123", filterValue);
    }

    private static Order CreateSampleOrder()
    {
        var order = new Order
        {
            Id = 42,
            UserId = "user-1",
            CreatedAt = new DateTime(2024, 5, 15, 10, 30, 0, DateTimeKind.Utc),
            PriceExclVat = 300m,
            Vat = 63m,
            Total = 363m,
            TotalPrice = 363m,
            PaymentConfirmation = "CONF123",
            InvoicePath = "INV-2024-001"
        };

        order.Items.Add(new OrderItem
        {
            Id = 1,
            OrderId = 42,
            CourseId = 100,
            Course = new Course { Id = 100, Title = "Course A" },
            Quantity = 1,
            UnitPriceExclVat = 150m,
            Vat = 31.5m,
            Total = 181.5m
        });

        order.Items.Add(new OrderItem
        {
            Id = 2,
            OrderId = 42,
            CourseId = 200,
            Course = new Course { Id = 200, Title = "Course B" },
            Quantity = 2,
            UnitPriceExclVat = 75m,
            Vat = 31.5m,
            Total = 181.5m
        });

        return order;
    }
}
