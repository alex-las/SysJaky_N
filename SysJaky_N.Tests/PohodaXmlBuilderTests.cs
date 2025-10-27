using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using SysJaky_N.Services.Pohoda;

namespace SysJaky_N.Tests;

public class PohodaXmlBuilderTests
{
    private static readonly PohodaXmlBuilder Builder = new(PohodaXmlSchemaProvider.DefaultSchemas);

    [Fact]
    public void BuildIssuedInvoiceXml_GeneratesExpectedStructureFromDto()
    {
        var invoice = CreateSampleInvoice();

        var xml = Builder.BuildIssuedInvoiceXml(invoice, "SysJaky_N");

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"windows-1250\"?>", xml, StringComparison.Ordinal);
        XmlTestHelper.AssertValidAgainstSchemas(xml);

        var document = XDocument.Parse(xml);
        var navigator = document.CreateNavigator();
        Assert.NotNull(navigator);

        var namespaces = CreateNamespaceManager(navigator!);

        Assert.Equal(
            "Invoice-2024-0001",
            EvaluateString(navigator!, namespaces, "/dat:dataPack/@id"));

        Assert.Equal(
            "issuedInvoice",
            EvaluateString(navigator!, namespaces, "/dat:dataPack/dat:dataPackItem/inv:invoice/inv:invoiceHeader/inv:invoiceType"));

        Assert.Equal(
            "Invoice 2024-0001",
            EvaluateString(navigator!, namespaces, "/dat:dataPack/dat:dataPackItem/inv:invoice/inv:invoiceHeader/inv:text"));

        Assert.Equal(
            "2024-05-20",
            EvaluateString(navigator!, namespaces, "/dat:dataPack/dat:dataPackItem/inv:invoice/inv:invoiceHeader/inv:date"));

        Assert.Equal(
            "high",
            EvaluateString(navigator!, namespaces, "(//inv:invoiceDetail/inv:invoiceItem/inv:rateVAT)[1]"));

        Assert.Equal(
            "none",
            EvaluateString(navigator!, namespaces, "(//inv:invoiceDetail/inv:invoiceItem/inv:rateVAT)[2]"));

        Assert.Equal(
            "low",
            EvaluateString(navigator!, namespaces, "(//inv:invoiceDetail/inv:invoiceItem/inv:rateVAT)[last()]"));

        Assert.Equal(
            "none",
            EvaluateString(navigator!, namespaces, "/dat:dataPack/dat:dataPackItem/inv:invoice/inv:invoiceSummary/inv:round"));

        Assert.Equal(
            "100",
            EvaluateString(navigator!, namespaces, "/dat:dataPack/dat:dataPackItem/inv:invoice/inv:invoiceSummary/inv:homeCurrency/typ:priceHigh"));

        Assert.Equal(
            "21",
            EvaluateString(navigator!, namespaces, "/dat:dataPack/dat:dataPackItem/inv:invoice/inv:invoiceSummary/inv:homeCurrency/typ:priceHighVAT"));

        Assert.Empty(SelectNodes(navigator!, namespaces, "//inv:invoiceDetail/inv:invoiceItem/inv:homeCurrency/typ:price"));
        Assert.Empty(SelectNodes(navigator!, namespaces, "//inv:invoiceDetail/inv:invoiceItem/inv:homeCurrency/typ:priceVAT"));
        Assert.Empty(SelectNodes(navigator!, namespaces, "//inv:invoiceDetail/inv:invoiceItem/inv:homeCurrency/typ:priceSum"));
    }

    private static InvoiceDto CreateSampleInvoice()
    {
        var header = new InvoiceHeader(
            InvoiceType: "issuedInvoice",
            OrderNumber: "2024-0001",
            Text: "Invoice 2024-0001",
            Date: new DateOnly(2024, 5, 20),
            TaxDate: new DateOnly(2024, 5, 20),
            DueDate: new DateOnly(2024, 6, 3),
            VariableSymbol: "20240001",
            SpecificSymbol: "SPEC-2024",
            Customer: new CustomerIdentity(
                Company: "ACME Corp",
                Name: "John Smith",
                Street: "Main Street 1",
                City: "Prague",
                Zip: "11000",
                Country: "CZ"),
            Note: "Thank you for your purchase");

        var items = new List<InvoiceItem>
        {
            new(
                Name: "Premium Course",
                Quantity: 1,
                UnitPriceExclVat: 100m,
                TotalExclVat: 100m,
                VatAmount: 21m,
                TotalInclVat: 121m,
                Discount: 0m,
                Rate: VatRate.High),
            new(
                Name: "VAT Exempt Service",
                Quantity: 2,
                UnitPriceExclVat: 50m,
                TotalExclVat: 100m,
                VatAmount: 0m,
                TotalInclVat: 100m,
                Discount: 0m,
                Rate: VatRate.None),
            new(
                Name: "Reduced VAT Item",
                Quantity: 1,
                UnitPriceExclVat: 80m,
                TotalExclVat: 80m,
                VatAmount: 8m,
                TotalInclVat: 88m,
                Discount: 5m,
                Rate: VatRate.Low)
        };

        return InvoiceDto.Create(
            header,
            items,
            totalExclVat: 280m,
            totalVat: 29m,
            totalInclVat: 309m,
            noneRateBase: 100m,
            lowRateBase: 80m,
            lowRateVat: 8m,
            highRateBase: 100m,
            highRateVat: 21m);
    }

    private static XmlNamespaceManager CreateNamespaceManager(XPathNavigator navigator)
    {
        var manager = new XmlNamespaceManager(navigator.NameTable);
        manager.AddNamespace("dat", "http://www.stormware.cz/schema/version_2/data.xsd");
        manager.AddNamespace("inv", "http://www.stormware.cz/schema/version_2/invoice.xsd");
        manager.AddNamespace("typ", "http://www.stormware.cz/schema/version_2/type.xsd");
        return manager;
    }

    private static string EvaluateString(XPathNavigator navigator, XmlNamespaceManager namespaces, string expression)
    {
        var result = navigator.Evaluate(expression, namespaces);
        return result switch
        {
            string value => value,
            XPathNodeIterator iterator when iterator.MoveNext() => iterator.Current?.Value ?? string.Empty,
            _ => Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static IReadOnlyCollection<string> SelectNodes(XPathNavigator navigator, XmlNamespaceManager namespaces, string expression)
    {
        var iterator = navigator.Select(expression, namespaces);
        var results = new List<string>();
        while (iterator.MoveNext())
        {
            if (!string.IsNullOrEmpty(iterator.Current?.Value))
            {
                results.Add(iterator.Current!.Value);
            }
            else
            {
                results.Add(string.Empty);
            }
        }

        return results;
    }
}
