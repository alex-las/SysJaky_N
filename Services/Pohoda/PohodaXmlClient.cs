using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaXmlClient : IPohodaClient
{
    private readonly HttpClient _httpClient;
    private readonly PohodaXmlOptions _options;
    private readonly PohodaXmlBuilder _builder;
    private readonly ILogger<PohodaXmlClient> _logger;
    private readonly PohodaPayloadSanitizer _payloadSanitizer;
    private readonly IPohodaMetrics _metrics;
    private readonly Encoding _encoding;
    private readonly PohodaResponseParser _responseParser;
    private readonly PohodaListRequestBuilder _listRequestBuilder;
    private readonly PohodaListParser _listParser;

    public PohodaXmlClient(
        HttpClient httpClient,
        IOptions<PohodaXmlOptions> options,
        PohodaXmlBuilder builder,
        PohodaResponseParser responseParser,
        PohodaListRequestBuilder listRequestBuilder,
        PohodaListParser listParser,
        ILogger<PohodaXmlClient> logger,
        PohodaPayloadSanitizer payloadSanitizer,
        IPohodaMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(responseParser);
        ArgumentNullException.ThrowIfNull(listRequestBuilder);
        ArgumentNullException.ThrowIfNull(listParser);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(payloadSanitizer);
        ArgumentNullException.ThrowIfNull(metrics);

        _httpClient = httpClient;
        _options = options.Value;
        _builder = builder;
        _responseParser = responseParser;
        _listRequestBuilder = listRequestBuilder;
        _listParser = listParser;
        _logger = logger;
        _payloadSanitizer = payloadSanitizer;
        _metrics = metrics;
        _encoding = CreateEncoding(_options.EncodingName);
    }

    public async Task<PohodaResponse> SendInvoiceAsync(
        string dataPack,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataPack);

        var scopedCorrelation = string.IsNullOrWhiteSpace(correlationId)
            ? $"pohoda-{Guid.NewGuid():N}"
            : correlationId;

        using var correlationScope = LogContext.PushProperty("CorrelationId", scopedCorrelation);

        var result = await SendDataPackAsync(dataPack, scopedCorrelation, true, cancellationToken)
            .ConfigureAwait(false);
        var parsed = result.Response;

        _logger.LogDebug(
            "Pohoda invoice sent successfully with document number {DocumentNumber} and ID {DocumentId}.",
            parsed.DocumentNumber,
            parsed.DocumentId);
        return parsed;
    }

    public async Task<IReadOnlyCollection<InvoiceStatus>> ListInvoicesAsync(
        PohodaListFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var request = _listRequestBuilder.Build(filter, applicationName: _options.Application);
        var correlationId = $"pohoda-list-{Guid.NewGuid():N}";
        using var correlationScope = LogContext.PushProperty("CorrelationId", correlationId);

        var result = await SendDataPackAsync(request, correlationId, false, cancellationToken)
            .ConfigureAwait(false);
        var invoices = _listParser.Parse(result.RawContent);

        _logger.LogDebug(
            "Retrieved {InvoiceCount} invoices from Pohoda using filters: number={Number}, symVar={SymVar}, from={DateFrom}, to={DateTo}.",
            invoices.Count,
            filter.Number ?? "",
            filter.VariableSymbol ?? "",
            filter.DateFrom?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "",
            filter.DateTo?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "");

        return invoices;
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

    private async Task<PohodaDataPackResult> SendDataPackAsync(
        string dataPack,
        string correlationId,
        bool trackMetrics,
        CancellationToken cancellationToken)
    {
        var endpoint = BuildEndpointUri("xml");
        var stopwatch = Stopwatch.StartNew();
        var requestSize = _encoding.GetByteCount(dataPack);
        HttpResponseMessage response;

        try
        {
            response = await SendWithRetryAsync(() => CreateRequest(HttpMethod.Post, endpoint, dataPack), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PohodaXmlException)
        {
            stopwatch.Stop();

            if (trackMetrics)
            {
                _metrics.ObserveExportFailure(stopwatch.Elapsed);
            }

            throw;
        }

        using (response)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            var responseSize = _encoding.GetByteCount(content);

            var payloadLog = await SanitizePayloadAsync(dataPack, content, correlationId, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Pohoda XML request completed with status {StatusCode} in {ElapsedMilliseconds} ms (request {RequestSizeBytes} B, response {ResponseSizeBytes} B).",
                (int)response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                requestSize,
                responseSize);

            if (!response.IsSuccessStatusCode)
            {
                if (trackMetrics)
                {
                    _metrics.ObserveExportFailure(stopwatch.Elapsed);
                }

                throw new PohodaXmlException(
                    $"Pohoda XML request failed with status {(int)response.StatusCode} {response.ReasonPhrase}.",
                    content,
                    payloadLog);
            }

            PohodaResponse parsed;

            try
            {
                parsed = _responseParser.Parse(content);
            }
            catch (PohodaXmlException ex)
            {
                if (trackMetrics)
                {
                    _metrics.ObserveExportFailure(stopwatch.Elapsed);
                }

                throw ex.PayloadLog is not null
                    ? ex
                    : new PohodaXmlException(ex.Message, ex.Payload ?? content, payloadLog, ex);
            }

            try
            {
                EnsureSuccess(parsed, content, payloadLog);
            }
            catch (PohodaXmlException)
            {
                if (trackMetrics)
                {
                    _metrics.ObserveExportFailure(stopwatch.Elapsed);
                }

                throw;
            }

            if (parsed.Warnings.Count > 0)
            {
                _logger.LogWarning(
                    "Pohoda response returned warnings: {Warnings}",
                    string.Join("; ", parsed.Warnings));
            }

            if (trackMetrics)
            {
                _metrics.ObserveExportSuccess(stopwatch.Elapsed);
            }

            var responseWithPayload = new PohodaResponse(
                parsed.State,
                parsed.DocumentNumber,
                parsed.DocumentId,
                parsed.Warnings,
                parsed.Errors,
                payloadLog);

            return new PohodaDataPackResult(content, responseWithPayload, payloadLog, stopwatch.Elapsed, requestSize, responseSize);
        }
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

    private static void EnsureSuccess(PohodaResponse response, string rawContent, PohodaPayloadLog? payloadLog)
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

        throw new PohodaXmlException(message, rawContent, payloadLog);
    }

    private async Task<PohodaPayloadLog?> SanitizePayloadAsync(
        string request,
        string response,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _payloadSanitizer.SaveAsync(request, response, correlationId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store sanitized Pohoda payloads for correlation {CorrelationId}.", correlationId);
            return null;
        }
    }

    private sealed record PohodaDataPackResult(
        string RawContent,
        PohodaResponse Response,
        PohodaPayloadLog? PayloadLog,
        TimeSpan Duration,
        int RequestSizeBytes,
        int ResponseSizeBytes);
}

public sealed class PohodaXmlException : Exception
{
    public PohodaXmlException(
        string message,
        string? payload = null,
        PohodaPayloadLog? payloadLog = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Payload = payload;
        PayloadLog = payloadLog;
    }

    public string? Payload { get; }

    public PohodaPayloadLog? PayloadLog { get; }
}
