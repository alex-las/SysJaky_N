using System.Xml;
using System.Xml.Linq;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaResponseParser
{
    private static readonly string[] NumberElementCandidates =
    [
        "numberAssigned",
        "numberRequested",
        "invoiceNumber",
        "number"
    ];

    private static readonly HashSet<string> WarningElementNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "warning",
        "warnings"
    };

    private static readonly HashSet<string> ErrorElementNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "error",
        "errors",
        "message"
    };

    public PohodaResponse Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new PohodaXmlException("Pohoda XML response was empty.", xml);
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (Exception ex) when (ex is XmlException or FormatException)
        {
            throw new PohodaXmlException("Failed to parse Pohoda XML response.", xml);
        }

        var root = document.Root ?? throw new PohodaXmlException("Pohoda XML response did not contain a root element.", xml);
        var responseItems = root
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "responsePackItem", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var state = DetermineState(root, responseItems);
        var documentNumber = ExtractDocumentNumber(root, responseItems);
        var documentId = ExtractDocumentId(responseItems);

        var entries = CollectMessageEntries(root, responseItems);
        var warnings = entries
            .Where(entry => entry.Severity == MessageSeverity.Warning)
            .Select(entry => entry.Message)
            .Distinct()
            .ToArray();
        var errors = entries
            .Where(entry => entry.Severity == MessageSeverity.Error)
            .Select(entry => entry.Message)
            .Distinct()
            .ToArray();

        return new PohodaResponse(
            state,
            documentNumber,
            documentId,
            warnings,
            errors);
    }

    private static string DetermineState(XElement root, IReadOnlyList<XElement> items)
    {
        var candidates = new List<StateCandidate>();
        AddStateCandidate(candidates, root.Attribute("state")?.Value);

        foreach (var item in items)
        {
            AddStateCandidate(candidates, item.Attribute("state")?.Value);
        }

        if (candidates.Count == 0)
        {
            return "ok";
        }

        var selected = candidates
            .OrderByDescending(c => c.Severity)
            .ThenBy(c => c.Index)
            .First();

        return string.IsNullOrWhiteSpace(selected.Original)
            ? "ok"
            : selected.Original;
    }

    private static void AddStateCandidate(ICollection<StateCandidate> candidates, string? state)
    {
        if (state is null)
        {
            return;
        }

        var normalized = NormalizeState(state);
        var severity = normalized switch
        {
            "error" or "fail" => 2,
            "warning" => 1,
            "ok" => 0,
            _ => 1
        };

        candidates.Add(new StateCandidate(state, severity, candidates.Count));
    }

    private static string? ExtractDocumentNumber(XElement root, IReadOnlyList<XElement> items)
    {
        foreach (var attributeName in new[] { "documentNumber", "number", "numberValue" })
        {
            foreach (var item in items)
            {
                var value = item.Attribute(attributeName)?.Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }

        foreach (var element in root
                     .Descendants()
                     .Where(e => NumberElementCandidates.Contains(e.Name.LocalName, StringComparer.OrdinalIgnoreCase)))
        {
            var value = element.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? ExtractDocumentId(IReadOnlyList<XElement> items)
    {
        foreach (var attributeName in new[] { "documentId" })
        {
            foreach (var item in items)
            {
                var value = item.Attribute(attributeName)?.Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }

        return null;
    }

    private static List<MessageEntry> CollectMessageEntries(XElement root, IReadOnlyList<XElement> items)
    {
        var entries = new List<MessageEntry>();

        var rootState = NormalizeState(root.Attribute("state")?.Value) ?? "ok";
        AddMessageEntry(entries, root.Attribute("stateDetail")?.Value, SeverityFromState(rootState));
        AddMessageEntry(entries, root.Attribute("stateInfo")?.Value, SeverityFromState(rootState));

        foreach (var item in items)
        {
            var itemState = NormalizeState(item.Attribute("state")?.Value) ?? rootState;

            AddMessageEntry(entries, item.Attribute("note")?.Value, SeverityFromState(itemState));
            AddMessageEntry(entries, item.Attribute("stateDetail")?.Value, SeverityFromState(itemState));
            AddMessageEntry(entries, item.Attribute("stateInfo")?.Value, SeverityFromState(itemState));

            foreach (var descendant in item.Descendants())
            {
                var localName = descendant.Name.LocalName;
                var text = descendant.Value;

                if (string.IsNullOrWhiteSpace(text))
                {
                    text = descendant.Attribute("message")?.Value;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (WarningElementNames.Contains(localName))
                {
                    AddMessageEntry(entries, text, MessageSeverity.Warning);
                }
                else if (ErrorElementNames.Contains(localName))
                {
                    AddMessageEntry(entries, text, MessageSeverity.Error);
                }
                else if (string.Equals(localName, "note", StringComparison.OrdinalIgnoreCase))
                {
                    AddMessageEntry(entries, text, SeverityFromState(itemState));
                }
            }
        }

        return entries;
    }

    private static void AddMessageEntry(ICollection<MessageEntry> entries, string? message, MessageSeverity severity)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        entries.Add(new MessageEntry(message.Trim(), severity));
    }

    private static MessageSeverity SeverityFromState(string state)
    {
        return state switch
        {
            "error" or "fail" => MessageSeverity.Error,
            "warning" => MessageSeverity.Warning,
            _ => MessageSeverity.Warning
        };
    }

    private static string? NormalizeState(string? state)
    {
        return string.IsNullOrWhiteSpace(state) ? null : state.Trim().ToLowerInvariant();
    }

    private readonly record struct MessageEntry(string Message, MessageSeverity Severity);

    private readonly record struct StateCandidate(string Original, int Severity, int Index);

    private enum MessageSeverity
    {
        Warning,
        Error
    }
}

public sealed class PohodaResponse
{
    public PohodaResponse(
        string state,
        string? documentNumber,
        string? documentId,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> errors)
    {
        State = string.IsNullOrWhiteSpace(state) ? "ok" : state;
        DocumentNumber = string.IsNullOrWhiteSpace(documentNumber) ? null : documentNumber;
        DocumentId = string.IsNullOrWhiteSpace(documentId) ? null : documentId;
        Warnings = warnings ?? Array.Empty<string>();
        Errors = errors ?? Array.Empty<string>();
    }

    public string State { get; }

    public string? DocumentNumber { get; }

    public string? DocumentId { get; }

    public IReadOnlyList<string> Warnings { get; }

    public IReadOnlyList<string> Errors { get; }
}
