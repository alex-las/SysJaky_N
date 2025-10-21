using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace SysJaky_N.Services.Pohoda;

public static class PohodaXmlSchemas
{
    private static readonly Lazy<IReadOnlyCollection<XmlSchema>> _defaultSchemas = new(LoadSchemas);

    public static IReadOnlyCollection<XmlSchema> Default => _defaultSchemas.Value;

    private static IReadOnlyCollection<XmlSchema> LoadSchemas()
    {
        var assembly = typeof(PohodaXmlSchemas).GetTypeInfo().Assembly;
        var resourcePrefix = $"{assembly.GetName().Name}.Resources.PohodaSchemas.";
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (resources.Length == 0)
        {
            throw new InvalidOperationException("No Pohoda XML schemas were embedded in the assembly.");
        }

        var schemas = new List<XmlSchema>(resources.Length);
        foreach (var resourceName in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });

            schemas.Add(XmlSchema.Read(reader, (sender, args) =>
            {
                throw new XmlSchemaException(args.Message, args.Exception);
            }) ?? throw new XmlSchemaException($"Failed to read schema '{resourceName}'."));
        }

        return schemas;
    }
}
