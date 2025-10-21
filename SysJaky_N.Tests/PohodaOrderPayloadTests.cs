using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Schema;
using SysJaky_N.Models;
using SysJaky_N.Services.Pohoda;

namespace SysJaky_N.Tests;

public class PohodaOrderPayloadTests
{
    [Fact]
    public void CreateInvoiceDataPack_BuildsMinimalInvoiceMatchingGoldenFile()
    {
        var order = CreateMinimalOrder();

        var mappedInvoice = OrderToInvoiceMapper.Map(order);
        var xml = PohodaOrderPayload.CreateInvoiceDataPack(mappedInvoice, "SysJaky_N");

        Assert.Contains("encoding=\"windows-1250\"", xml, StringComparison.OrdinalIgnoreCase);

        XmlTestHelper.AssertValidAgainstSchemas(xml);
        XmlTestHelper.AssertEqualIgnoringWhitespace(XmlTestHelper.LoadPohodaSample("invoice_minimal.xml"), xml);
    }

    [Fact]
    public void CreateInvoiceDataPack_BuildsInvoiceWithDetailAndSummary()
    {
        var order = CreateSampleOrder();

        var mappedInvoice = OrderToInvoiceMapper.Map(order);
        var xml = PohodaOrderPayload.CreateInvoiceDataPack(mappedInvoice, "SysJaky_N");

        Assert.Contains("encoding=\"windows-1250\"", xml, StringComparison.OrdinalIgnoreCase);

        XmlTestHelper.AssertValidAgainstSchemas(xml);
        XmlTestHelper.AssertEqualIgnoringWhitespace(XmlTestHelper.LoadPohodaSample("invoice_full.xml"), xml);
    }

    [Fact]
    public void CreateInvoiceDataPack_DistributesDiscount()
    {
        var order = CreateSampleOrder();
        order.TotalPrice = 400m;
        order.Total = 363m;

        var mappedInvoice = OrderToInvoiceMapper.Map(order);
        var xml = PohodaOrderPayload.CreateInvoiceDataPack(mappedInvoice, "SysJaky_N");

        XmlTestHelper.AssertValidAgainstSchemas(xml);

        var document = XDocument.Parse(xml);
        var detail = document.Root!
            .Element("{http://www.stormware.cz/schema/version_2/data.xsd}dataPackItem")!
            .Element("{http://www.stormware.cz/schema/version_2/invoice.xsd}invoice")!
            .Element("{http://www.stormware.cz/schema/version_2/invoice.xsd}invoiceDetail");

        var discountElements = detail!
            .Elements("{http://www.stormware.cz/schema/version_2/invoice.xsd}invoiceItem")
            .Select(item => item.Element("{http://www.stormware.cz/schema/version_2/invoice.xsd}discountPercentage")?.Value)
            .Where(value => value is not null)
            .Select(value => decimal.Parse(value!, CultureInfo.InvariantCulture))
            .ToList();

        Assert.NotEmpty(discountElements);
        Assert.True(discountElements.Sum() > 0m);
    }

    [Fact]
    public void ValidateAgainstXsd_ThrowsForInvalidVatRate()
    {
        var invalidXml = XmlTestHelper.LoadPohodaSample("invoice_minimal.xml");
        var document = XDocument.Parse(invalidXml);
        var rateElement = document
            .Descendants("{http://www.stormware.cz/schema/version_2/invoice.xsd}rateVAT")
            .Single();
        rateElement.Value = "invalid";

        var tamperedXml = document.ToString(SaveOptions.DisableFormatting);

        Assert.Throws<XmlSchemaValidationException>(() =>
            PohodaOrderPayload.ValidateAgainstXsd(tamperedXml, PohodaXmlSchemaProvider.DefaultSchemas));
    }

    private static Order CreateMinimalOrder()
    {
        var order = new Order
        {
            Id = 1,
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PriceExclVat = 100m,
            Vat = 21m,
            Total = 121m,
            TotalPrice = 121m
        };

        order.Items.Add(new OrderItem
        {
            Id = 1,
            OrderId = 1,
            CourseId = 10,
            Course = new Course { Id = 10, Title = "Minimal Course" },
            Quantity = 1,
            UnitPriceExclVat = 100m,
            Vat = 21m,
            Total = 121m
        });

        return order;
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
