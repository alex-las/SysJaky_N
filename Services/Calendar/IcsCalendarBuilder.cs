using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace SysJaky_N.Services.Calendar;

public interface IIcsCalendarBuilderFactory
{
    IcsCalendarBuilder Create(string? calendarName = null, bool includeGregorianCalendar = false);
}

public class IcsCalendarBuilderFactory : IIcsCalendarBuilderFactory
{
    private readonly string _productId;

    public IcsCalendarBuilderFactory(string productId = "-//SysJaky_N//EN")
    {
        _productId = productId;
    }

    public IcsCalendarBuilder Create(string? calendarName = null, bool includeGregorianCalendar = false)
    {
        return new IcsCalendarBuilder(_productId, calendarName, includeGregorianCalendar);
    }
}

public class IcsCalendarBuilder
{
    private readonly StringBuilder _builder = new();
    private bool _isFinalized;

    internal IcsCalendarBuilder(string productId, string? calendarName, bool includeGregorianCalendar)
    {
        _builder.AppendLine("BEGIN:VCALENDAR");
        _builder.AppendLine("VERSION:2.0");
        _builder.AppendLine($"PRODID:{productId}");

        if (includeGregorianCalendar)
        {
            _builder.AppendLine("CALSCALE:GREGORIAN");
        }

        if (!string.IsNullOrWhiteSpace(calendarName))
        {
            _builder.AppendLine($"X-WR-CALNAME:{EscapeText(calendarName)}");
        }
    }

    public IcsCalendarBuilder AddEvent(IcsCalendarEvent calendarEvent)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        if (calendarEvent.End < calendarEvent.Start)
        {
            throw new ArgumentException("Event end must be on or after the start.", nameof(calendarEvent));
        }

        _builder.AppendLine("BEGIN:VEVENT");
        _builder.AppendLine($"UID:{calendarEvent.Uid}");
        var timestamp = calendarEvent.Timestamp ?? DateTime.UtcNow;
        _builder.AppendLine($"DTSTAMP:{FormatUtcDateTime(timestamp)}");

        if (calendarEvent.IsAllDay)
        {
            var startDate = calendarEvent.Start.Date;
            var endDate = calendarEvent.End.Date;
            _builder.AppendLine($"DTSTART;VALUE=DATE:{startDate:yyyyMMdd}");
            _builder.AppendLine($"DTEND;VALUE=DATE:{endDate:yyyyMMdd}");
        }
        else if (!string.IsNullOrWhiteSpace(calendarEvent.TimeZoneId))
        {
            _builder.AppendLine($"DTSTART;TZID={calendarEvent.TimeZoneId}:{FormatDateTime(calendarEvent.Start)}");
            _builder.AppendLine($"DTEND;TZID={calendarEvent.TimeZoneId}:{FormatDateTime(calendarEvent.End)}");
        }
        else
        {
            _builder.AppendLine($"DTSTART:{FormatUtcDateTime(calendarEvent.Start)}");
            _builder.AppendLine($"DTEND:{FormatUtcDateTime(calendarEvent.End)}");
        }

        _builder.AppendLine($"SUMMARY:{EscapeText(calendarEvent.Summary)}");
        if (!string.IsNullOrWhiteSpace(calendarEvent.Description))
        {
            _builder.AppendLine($"DESCRIPTION:{EscapeText(calendarEvent.Description)}");
        }
        _builder.AppendLine("END:VEVENT");

        return this;
    }

    public string BuildString()
    {
        FinalizeCalendar();
        return _builder.ToString();
    }

    public byte[] BuildBytes()
    {
        return Encoding.UTF8.GetBytes(BuildString());
    }

    public FileContentHttpResult BuildFile(string fileName)
    {
        var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "calendar.ics" : fileName;
        return TypedResults.File(BuildBytes(), "text/calendar", safeFileName);
    }

    private void FinalizeCalendar()
    {
        if (_isFinalized)
        {
            return;
        }

        _builder.AppendLine("END:VCALENDAR");
        _isFinalized = true;
    }

    internal static string EscapeText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace(",", "\\,")
            .Replace(";", "\\;");
    }

    private static string FormatDateTime(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
        {
            value = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        }

        return value.ToString("yyyyMMddTHHmmss");
    }

    internal static string FormatUtcDateTime(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
        {
            value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
        else
        {
            value = value.ToUniversalTime();
        }

        return value.ToString("yyyyMMddTHHmmssZ");
    }
}

public class IcsCalendarEvent
{
    public IcsCalendarEvent(string uid, string summary, DateTime start, DateTime end)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            throw new ArgumentException("Event UID is required.", nameof(uid));
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Event summary is required.", nameof(summary));
        }

        Uid = uid;
        Summary = summary;
        Start = start;
        End = end;
    }

    public string Uid { get; }

    public string Summary { get; }

    public DateTime Start { get; }

    public DateTime End { get; }

    public DateTime? Timestamp { get; set; }

    public string? Description { get; set; }

    public bool IsAllDay { get; set; }

    public string? TimeZoneId { get; set; }
}
