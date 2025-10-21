using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaXmlClient
{
    private static readonly Encoding Windows1250Encoding = CreateEncoding();

    private readonly HttpClient _httpClient;
    private readonly PohodaXmlOptions _options;
    private readonly ILogger<PohodaXmlClient> _logger;

    public PohodaXmlClient(HttpClient httpClient, IOptions<PohodaXmlOptions> options, ILogger<PohodaXmlClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PohodaXmlResponse> SendInvoiceAsync(string dataPack, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataPack);

        var endpoint = BuildEndpointUri("xml");
        using var request = CreateRequest(HttpMethod.Post, endpoint, dataPack);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new PohodaXmlException($"Pohoda XML request failed with status {(int)response.StatusCode} {response.ReasonPhrase}.", content);
        }

        var parsed = ParseResponse(content);
        _logger.LogDebug("Pohoda invoice sent successfully with invoice number {InvoiceNumber}.", parsed.InvoiceNumber);
        return parsed;
    }

    public async Task<PohodaXmlResponse> ListInvoiceAsync(string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(externalId);

        var dataPack = PohodaOrderPayload.CreateListInvoiceRequest(externalId, _options.Application);
        return await SendInvoiceAsync(dataPack, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CheckStatusAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = BuildEndpointUri("status");
        using var request = CreateRequest(HttpMethod.Get, endpoint, content: null);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string? content)
    {
        var request = new HttpRequestMessage(method, uri);

        if (content is not null)
        {
            request.Content = new StringContent(content, Windows1250Encoding, "text/xml");
        }

        var credentials = Convert.ToBase64String(Windows1250Encoding.GetBytes($"{_options.Username}:{_options.Password}"));
        request.Headers.TryAddWithoutValidation("STW-Authorization", credentials);

        if (!string.IsNullOrWhiteSpace(_options.Application))
        {
            request.Headers.TryAddWithoutValidation("STW-Application", _options.Application);
        }

        if (!string.IsNullOrWhiteSpace(_options.Instance))
        {
            request.Headers.TryAddWithoutValidation("STW-Instance", _options.Instance);
        }

        request.Headers.TryAddWithoutValidation("STW-Check-Duplicity", _options.CheckDuplicity ? "true" : "false");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        return request;
    }

    private static Encoding CreateEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding("windows-1250");
    }

    private Uri BuildEndpointUri(string relativePath)
    {
        var baseUri = new Uri(_options.BaseUrl.EndsWith('/') ? _options.BaseUrl : _options.BaseUrl + "/", UriKind.Absolute);
        return new Uri(baseUri, relativePath);
    }

    private static PohodaXmlResponse ParseResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new PohodaXmlException("Pohoda XML response was empty.", content);
        }

        var document = XDocument.Parse(content);
        var root = document.Root ?? throw new PohodaXmlException("Pohoda XML response did not contain a root element.", content);

        var state = (string?)root.Attribute("state") ?? string.Empty;
        if (!string.Equals(state, "ok", StringComparison.OrdinalIgnoreCase))
        {
            var message = (string?)root.Attribute("stateDetail") ?? (string?)root.Attribute("stateInfo") ?? "Unknown error";
            throw new PohodaXmlException($"Pohoda XML response returned state '{state}'. {message}", content);
        }

        var invoiceNumber = ExtractInvoiceNumber(root);
        return new PohodaXmlResponse(invoiceNumber, content);
    }

    private static string? ExtractInvoiceNumber(XElement root)
    {
        var numberElement = root
            .Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "numberAssigned", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(e.Name.LocalName, "numberRequested", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(e.Name.LocalName, "invoiceNumber", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(e.Name.LocalName, "number", StringComparison.OrdinalIgnoreCase));

        return numberElement?.Value.Trim();
    }
}

public sealed class PohodaXmlResponse
{
    public PohodaXmlResponse(string? invoiceNumber, string rawContent)
    {
        InvoiceNumber = string.IsNullOrWhiteSpace(invoiceNumber) ? null : invoiceNumber;
        RawContent = rawContent;
    }

    public string? InvoiceNumber { get; }

    public string RawContent { get; }
}

public sealed class PohodaXmlException : Exception
{
    public PohodaXmlException(string message, string? payload = null)
        : base(message)
    {
        Payload = payload;
    }

    public string? Payload { get; }
}
