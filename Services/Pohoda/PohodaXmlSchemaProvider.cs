using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Schema;

namespace SysJaky_N.Services.Pohoda;

public static class PohodaXmlSchemaProvider
{
    private static readonly Lazy<IReadOnlyCollection<XmlSchema>> LazySchemas = new(LoadDefaultSchemas);

    public static IReadOnlyCollection<XmlSchema> DefaultSchemas => LazySchemas.Value;

    private static IReadOnlyCollection<XmlSchema> LoadDefaultSchemas()
    {
        var assembly = typeof(PohodaXmlSchemaProvider).GetTypeInfo().Assembly;
        var resourceNames = new[]
        {
            "SysJaky_N.Services.Pohoda.Schemas.data.xsd",
            "SysJaky_N.Services.Pohoda.Schemas.invoice.xsd",
            "SysJaky_N.Services.Pohoda.Schemas.list.xsd",
            "SysJaky_N.Services.Pohoda.Schemas.filter.xsd",
            "SysJaky_N.Services.Pohoda.Schemas.type.xsd"
        };

        var schemaSet = new XmlSchemaSet();
        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded schema '{resourceName}' was not found.");

            var schema = XmlSchema.Read(stream, null)
                ?? throw new InvalidOperationException($"Failed to load XML schema '{resourceName}'.");

            schemaSet.Add(schema);
        }

        schemaSet.Compile();
        return schemaSet.Schemas().Cast<XmlSchema>().ToList().AsReadOnly();
    }
}
