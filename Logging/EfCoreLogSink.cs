using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Serilog.Core;
using Serilog.Events;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Logging;

internal sealed class EfCoreLogSink : ILogEventSink
{
    private readonly IDbContextFactory<ApplicationDbContext> _ctxFactory;

    private static readonly AsyncLocal<bool> _isLogging = new();

    internal EfCoreLogSink(IDbContextFactory<ApplicationDbContext> ctxFactory)
        => _ctxFactory = ctxFactory;

    public void Emit(LogEvent logEvent)
    {
        if (ShouldSkipLogging())
        {
            return;
        }

        _isLogging.Value = true;

        try
        {
            using var ctx = _ctxFactory.CreateDbContext();

            var entry = new LogEntry
            {
                Timestamp = logEvent.Timestamp.UtcDateTime,
                Level = logEvent.Level.ToString(),
                Message = logEvent.RenderMessage(),
                Exception = logEvent.Exception?.ToString(),
                Properties = SerializeProperties(logEvent.Properties),
                CorrelationId = ExtractScalarValue(logEvent.Properties, "CorrelationId"),
                SourceContext = ExtractScalarValue(logEvent.Properties, "SourceContext")
            };

            ctx.LogEntries.Add(entry);
            ctx.SaveChanges();
        }
        catch
        {
            // Schltnout – logování nesmí nikdy shodit appku
        }
        finally
        {
            _isLogging.Value = false;
        }
    }

    private static string? SerializeProperties(IReadOnlyDictionary<string, LogEventPropertyValue> properties)
    {
        if (properties.Count == 0) return null;
        var dict = properties.ToDictionary(p => p.Key, p => p.Value.ToString());
        return JsonSerializer.Serialize(dict);
    }

    private static string? ExtractScalarValue(IReadOnlyDictionary<string, LogEventPropertyValue> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value)) return null;
        return value is ScalarValue s ? s.Value?.ToString() : value.ToString();
    }

    private static bool ShouldSkipLogging()
    {
        if (_isLogging.Value)
        {
            return true;
        }

        var disableLogsValue = Environment.GetEnvironmentVariable("DISABLE_DB_LOGS");
        if (string.IsNullOrWhiteSpace(disableLogsValue))
        {
            return false;
        }

        return disableLogsValue.Equals("1", StringComparison.OrdinalIgnoreCase)
            || disableLogsValue.Equals("true", StringComparison.OrdinalIgnoreCase)
            || disableLogsValue.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || disableLogsValue.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}
