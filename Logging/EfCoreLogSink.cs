using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Logging;

public class EfCoreLogSink : ILogEventSink
{
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly AsyncLocal<bool> _isLogging = new();

    public EfCoreLogSink(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void Emit(LogEvent logEvent)
    {
        if (ShouldSkipLogging())
        {
            return;
        }

        try
        {
            _isLogging.Value = true;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

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

            context.LogEntries.Add(entry);
            context.SaveChanges();
        }
        catch
        {
            // Ignore logging failures so they do not affect the main request pipeline.
        }
        finally
        {
            _isLogging.Value = false;
        }
    }

    private static string? SerializeProperties(IReadOnlyDictionary<string, LogEventPropertyValue> properties)
    {
        if (properties.Count == 0)
        {
            return null;
        }

        var serialized = properties.ToDictionary(
            p => p.Key,
            p => p.Value.ToString());

        return JsonSerializer.Serialize(serialized);
    }

    private static string? ExtractScalarValue(IReadOnlyDictionary<string, LogEventPropertyValue> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value))
        {
            return null;
        }

        if (value is ScalarValue scalar)
        {
            return scalar.Value?.ToString();
        }

        return value.ToString();
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

