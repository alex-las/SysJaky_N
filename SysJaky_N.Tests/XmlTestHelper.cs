using System;
using System.IO;
using System.Xml.Linq;
using SysJaky_N.Services.Pohoda;

namespace SysJaky_N.Tests;

internal static class XmlTestHelper
{
    public static string LoadPohodaSample(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Pohoda", fileName);
        return File.ReadAllText(path);
    }

    public static string Normalize(string xml)
    {
        var document = XDocument.Parse(xml);
        return document.ToString(SaveOptions.DisableFormatting);
    }

    public static void AssertEqualIgnoringWhitespace(string expectedXml, string actualXml)
        => Assert.Equal(Normalize(expectedXml), Normalize(actualXml));

    public static void AssertValidAgainstSchemas(string xml)
        => PohodaOrderPayload.ValidateAgainstXsd(xml, PohodaXmlSchemaProvider.DefaultSchemas);
}
