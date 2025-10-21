using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaXmlClient
{
    private readonly HttpClient _httpClient;
    private readonly PohodaXmlOptions _options;
    private readonly PohodaXmlBuilder _builder;
    private readonly ILogger<PohodaXmlClient> _logger;
    private readonly Encoding _encoding;
    private readonly PohodaResponseParser _responseParser;

    public PohodaXmlClient(
        HttpClient httpClient,
        IOptions<PohodaXmlOptions> options,
        PohodaXmlBuilder builder,
        PohodaResponseParser responseParser,
        ILogger<PohodaXmlClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(responseParser);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _options = options.Value;
        _builder = builder;
        _responseParser = responseParser;
        _logger = logger;
        _encoding = CreateEncoding(_options.EncodingName);
    }

    public async Task<PohodaResponse> SendInvoiceAsync(string dataPack, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataPack);

        var endpoint = BuildEndpointUri("xml");
        using var response = await SendWithRetryAsync(() => CreateRequest(HttpMethod.Post, endpoint, dataPack), cancellationToken)
            .ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new PohodaXmlException($"Pohoda XML request failed with status {(int)response.StatusCode} {response.ReasonPhrase}.", content);
        }

        var parsed = _responseParser.Parse(content);
        EnsureSuccess(parsed, content);

        if (parsed.Warnings.Count > 0)
        {
            _logger.LogWarning(
                "Pohoda response returned warnings: {Warnings}",
                string.Join("; ", parsed.Warnings));
        }

        _logger.LogDebug(
            "Pohoda invoice sent successfully with document number {DocumentNumber} and ID {DocumentId}.",
            parsed.DocumentNumber,
            parsed.DocumentId);
        return parsed;
    }

    public async Task<PohodaResponse> ListInvoiceAsync(string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(externalId);

        var dataPack = _builder.BuildListInvoiceRequest(externalId, _options.Application);
        return await SendInvoiceAsync(dataPack, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CheckStatusAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = BuildEndpointUri("status");
        using var response = await SendWithRetryAsync(() => CreateRequest(HttpMethod.Get, endpoint, content: null), cancellationToken)
            .ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string? content)
    {
        var request = new HttpRequestMessage(method, uri);

        if (content is not null)
        {
            request.Content = new StringContent(content, _encoding, "text/xml");
        }

        var credentials = Convert.ToBase64String(_encoding.GetBytes($"{_options.Username}:{_options.Password}"));
        request.Headers.TryAddWithoutValidation("STW-Authorization", $"Basic {credentials}");

        if (!string.IsNullOrWhiteSpace(_options.Application))
        {
            request.Headers.TryAddWithoutValidation("STW-Application", _options.Application);
        }

        if (!string.IsNullOrWhiteSpace(_options.Instance))
        {
            request.Headers.TryAddWithoutValidation("STW-Instance", _options.Instance);
        }

        if (!string.IsNullOrWhiteSpace(_options.Company))
        {
            request.Headers.TryAddWithoutValidation("STW-Company", _options.Company);
        }

        request.Headers.TryAddWithoutValidation("STW-Check-Duplicity", _options.CheckDuplicity ? "true" : "false");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        return request;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        var totalAttempts = Math.Max(0, _options.RetryCount) + 1;

        for (var attempt = 1; attempt <= totalAttempts; attempt++)
        {
            var request = requestFactory();
            try
            {
                var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode || attempt == totalAttempts || !ShouldRetry(response))
                {
                    return response;
                }

                _logger.LogWarning(
                    "Pohoda XML request failed with status {StatusCode}. Retrying attempt {Attempt} of {TotalAttempts}.",
                    (int)response.StatusCode,
                    attempt,
                    totalAttempts);
                response.Dispose();
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < totalAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Transient error when calling Pohoda XML (attempt {Attempt} of {TotalAttempts}).",
                    attempt,
                    totalAttempts);
            }
            finally
            {
                request.Dispose();
            }

            if (attempt < totalAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new PohodaXmlException("Pohoda XML request failed after all retry attempts.");
    }

    private static bool ShouldRetry(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        if (statusCode == (int)HttpStatusCode.RequestTimeout)
        {
            return true;
        }

        return statusCode >= 500 && statusCode != (int)HttpStatusCode.NotImplemented;
    }

    private static bool IsTransient(Exception exception)
    {
        return exception is HttpRequestException or TaskCanceledException;
    }

    private static Encoding CreateEncoding(string encodingName)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var fallbackEncoding = "windows-1250";
        var name = string.IsNullOrWhiteSpace(encodingName) ? fallbackEncoding : encodingName;

        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            return Encoding.GetEncoding(fallbackEncoding);
        }
    }

    private Uri BuildEndpointUri(string relativePath)
    {
        var baseUri = new Uri(_options.BaseUrl.EndsWith('/') ? _options.BaseUrl : _options.BaseUrl + "/", UriKind.Absolute);
        return new Uri(baseUri, relativePath);
    }

    private static void EnsureSuccess(PohodaResponse response, string rawContent)
    {
        static bool IsSuccessState(string state)
            => string.Equals(state, "ok", StringComparison.OrdinalIgnoreCase)
               || string.Equals(state, "warning", StringComparison.OrdinalIgnoreCase);

        if (IsSuccessState(response.State) && response.Errors.Count == 0)
        {
            return;
        }

        var message = response.Errors.Count > 0
            ? string.Join("; ", response.Errors)
            : $"Pohoda XML response returned state '{response.State}'.";

        throw new PohodaXmlException(message, rawContent);
    }
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
