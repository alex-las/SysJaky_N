using System;
using System.IO;
using System.Xml.Linq;
using SysJaky_N.Services.Pohoda;
using Xunit;

namespace SysJaky_N.Tests;

public class PohodaListRequestBuilderTests
{
    private static readonly PohodaListRequestBuilder Builder = new(PohodaXmlSchemaProvider.DefaultSchemas);

    [Fact]
    public void Build_GeneratesExpectedXml()
    {
        var filter = new PohodaListFilter
        {
            Number = "INV-123",
            VariableSymbol = "42",
            DateFrom = new DateOnly(2024, 1, 15),
            DateTo = new DateOnly(2024, 1, 15)
        };

        var xml = Builder.Build(filter, applicationName: "SysJaky_N");
        var document = XDocument.Parse(xml);

        var samplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "SampleListInvoiceRequest.xml");
        var expected = XDocument.Load(samplePath);

        Assert.True(XNode.DeepEquals(expected.Root, document.Root));
    }

    [Fact]
    public void BuildFilter_IncludesProvidedValues()
    {
        var filter = new PohodaListFilter
        {
            Number = "INV-999",
            VariableSymbol = "100",
            DateFrom = new DateOnly(2024, 6, 1),
            DateTo = new DateOnly(2024, 6, 30)
        };

        var filterElement = Builder.BuildFilter(filter);

        Assert.Equal("INV-999", filterElement.Element(BuilderNamespaces.Filter + "number")?.Value);
        Assert.Equal("2024-06-01", filterElement.Element(BuilderNamespaces.Filter + "dateFrom")?.Value);
        Assert.Equal("2024-06-30", filterElement.Element(BuilderNamespaces.Filter + "dateTill")?.Value);
        Assert.Equal("100", filterElement.Element(BuilderNamespaces.Filter + "symVar")?.Value);
    }

    private static class BuilderNamespaces
    {
        public static readonly XNamespace Filter = "http://www.stormware.cz/schema/version_2/filter.xsd";
    }
}
