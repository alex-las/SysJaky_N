using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SysJaky_N.Services.Pohoda;

public class PohodaPayloadSanitizer
{
    private static readonly HashSet<string> SensitiveElementNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "company",
        "name",
        "street",
        "city",
        "zip",
        "country",
        "email",
        "phone",
        "mobile",
        "ico",
        "dic",
        "vatid",
        "variableSymbol",
        "symVar",
        "accountNumber",
        "iban",
        "swift"
    };

    private readonly string _payloadDirectory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PohodaPayloadSanitizer> _logger;

    public PohodaPayloadSanitizer(IHostEnvironment environment, TimeProvider timeProvider, ILogger<PohodaPayloadSanitizer> logger)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _timeProvider = timeProvider;
        _logger = logger;

        var root = string.IsNullOrWhiteSpace(environment.ContentRootPath)
            ? AppContext.BaseDirectory
            : environment.ContentRootPath;

        _payloadDirectory = Path.Combine(root, "Logs", "PohodaPayloads");
    }

    public async Task<PohodaPayloadLog> SaveAsync(
        string? requestXml,
        string? responseXml,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var safeCorrelation = NormalizeCorrelationId(correlationId);
        var timestamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        Directory.CreateDirectory(_payloadDirectory);

        var requestPath = await SanitizeAndPersistAsync(requestXml, timestamp, safeCorrelation, "request", cancellationToken)
            .ConfigureAwait(false);
        var responsePath = await SanitizeAndPersistAsync(responseXml, timestamp, safeCorrelation, "response", cancellationToken)
            .ConfigureAwait(false);

        return new PohodaPayloadLog(requestPath, responsePath);
    }

    public string Sanitize(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return string.Empty;
        }

        try
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            foreach (var element in document.Descendants())
            {
                if (SensitiveElementNames.Contains(element.Name.LocalName))
                {
                    element.Value = "[redacted]";
                }
            }

            var declaration = document.Declaration?.ToString();
            return declaration is null
                ? document.ToString(SaveOptions.DisableFormatting)
                : declaration + Environment.NewLine + document.ToString(SaveOptions.DisableFormatting);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sanitize Pohoda XML payload.");
            return "<!-- sanitization_failed -->";
        }
    }

    private async Task<string?> SanitizeAndPersistAsync(
        string? payload,
        string timestamp,
        string correlationId,
        string suffix,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var sanitized = Sanitize(payload);
        var fileName = $"{timestamp}-{correlationId}-{suffix}.xml";
        var path = Path.Combine(_payloadDirectory, fileName);

        try
        {
            await File.WriteAllTextAsync(path, sanitized, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist sanitized Pohoda XML payload to {Path}.", path);
            return null;
        }
    }

    private static string NormalizeCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return "payload";
        }

        var filtered = correlationId
            .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
            .Take(60)
            .ToArray();

        return filtered.Length == 0 ? "payload" : new string(filtered);
    }
}
