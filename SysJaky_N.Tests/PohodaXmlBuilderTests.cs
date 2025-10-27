using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml.XPath;
using SysJaky_N.Services.Pohoda;

namespace SysJaky_N.Tests;

public class PohodaXmlBuilderTests
{
    private static readonly PohodaXmlBuilder Builder = new(PohodaXmlSchemaProvider.DefaultSchemas);

    [Fact]
    public void BuildIssuedInvoiceXml_UsesInvoiceDtoSampleAndProducesExpectedStructure()
    {
        var invoice = CreateInvoiceSample();

        var xml = Builder.BuildIssuedInvoiceXml(invoice, "SysJaky_N");

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"windows-1250\"?>", xml, StringComparison.Ordinal);

        XmlTestHelper.AssertValidAgainstSchemas(xml);

        var document = XDocument.Parse(xml);
        var navigator = document.CreateNavigator();
        var manager = new XmlNamespaceManager(navigator.NameTable);
        manager.AddNamespace("dat", "http://www.stormware.cz/schema/version_2/data.xsd");
        manager.AddNamespace("inv", "http://www.stormware.cz/schema/version_2/invoice.xsd");
        manager.AddNamespace("typ", "http://www.stormware.cz/schema/version_2/type.xsd");

        string Evaluate(string expression)
        {
            var result = navigator.Evaluate(expression, manager);
            return result switch
            {
                string stringResult => stringResult,
                _ => navigator.Evaluate("string(" + expression + ")", manager) as string ?? string.Empty
            };
        }

        Assert.Equal("issuedInvoice", Evaluate("string(/dat:dataPack/dat:dataPackItem/inv:invoice/inv:invoiceHeader/inv:invoiceType)"));
        Assert.Equal("Objednávka 42", Evaluate("string(/dat:dataPack/dat:dataPackItem/inv:invoice/inv:invoiceHeader/inv:text)"));
        Assert.Equal("high", Evaluate("string(/dat:dataPack/dat:dataPackItem/inv:invoice/inv:invoiceDetail/inv:invoiceItem[1]/inv:rateVAT)"));
        Assert.Equal("low", Evaluate("string(/dat:dataPack/dat:dataPackItem/inv:invoice/inv:invoiceDetail/inv:invoiceItem[2]/inv:rateVAT)"));
        Assert.Equal("none", Evaluate("string(/dat:dataPack/dat:dataPackItem/inv:invoice/inv:invoiceSummary/inv:round)"));

        var typNamespace = XNamespace.Get("http://www.stormware.cz/schema/version_2/type.xsd");

        Assert.Empty(document.Descendants(typNamespace + "price"));
        Assert.Empty(document.Descendants(typNamespace + "priceVAT"));
        Assert.Empty(document.Descendants(typNamespace + "priceSum"));
    }

    private static InvoiceDto CreateInvoiceSample()
    {
        var header = new InvoiceHeader(
            InvoiceType: "issuedInvoice",
            OrderNumber: "42",
            Text: "Objednávka 42",
            Date: new DateOnly(2024, 5, 15),
            TaxDate: new DateOnly(2024, 5, 15),
            DueDate: new DateOnly(2024, 5, 29),
            VariableSymbol: "42",
            SpecificSymbol: "CONF123",
            Customer: new CustomerIdentity("user-1", null, null, null, null, null),
            Note: "INV-2024-001");

        var items = new List<InvoiceItem>
        {
            new(
                Name: "Course A",
                Quantity: 1,
                UnitPriceExclVat: 150m,
                TotalExclVat: 150m,
                VatAmount: 31.5m,
                TotalInclVat: 181.5m,
                Discount: 0m,
                Rate: VatRate.High),
            new(
                Name: "Course B",
                Quantity: 2,
                UnitPriceExclVat: 50m,
                TotalExclVat: 100m,
                VatAmount: 10m,
                TotalInclVat: 110m,
                Discount: 0m,
                Rate: VatRate.Low)
        };

        return InvoiceDto.Create(
            header,
            items,
            totalExclVat: 250m,
            totalVat: 41.5m,
            totalInclVat: 291.5m,
            noneRateBase: null,
            lowRateBase: 100m,
            lowRateVat: 10m,
            highRateBase: 150m,
            highRateVat: 31.5m);
    }
}
