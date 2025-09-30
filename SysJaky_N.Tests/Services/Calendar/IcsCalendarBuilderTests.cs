using System.Linq;
using Microsoft.AspNetCore.Http.HttpResults;
using SysJaky_N.Services.Calendar;
using Xunit;

namespace SysJaky_N.Tests.Services.Calendar;

public class IcsCalendarBuilderTests
{
    private readonly IIcsCalendarBuilderFactory _factory = new IcsCalendarBuilderFactory();

    [Fact]
    public void Build_AllDayEvent_WithoutDescription_OmitsDescriptionLine()
    {
        // Arrange
        var builder = _factory.Create();
        var calendarEvent = new IcsCalendarEvent("1@sysjaky_n", "All Day", new DateTime(2024, 5, 1), new DateTime(2024, 5, 2))
        {
            IsAllDay = true,
            Timestamp = new DateTime(2024, 4, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        builder.AddEvent(calendarEvent);
        var ics = builder.BuildString();

        // Assert
        Assert.Contains("DTSTART;VALUE=DATE:20240501", ics);
        Assert.Contains("DTEND;VALUE=DATE:20240502", ics);
        Assert.DoesNotContain("DESCRIPTION:", ics);
    }

    [Fact]
    public void Build_EventWithDescription_EscapesSpecialCharacters()
    {
        // Arrange
        var builder = _factory.Create();
        var calendarEvent = new IcsCalendarEvent("2@sysjaky_n", "Summary, Test", new DateTime(2024, 6, 1, 8, 30, 0, DateTimeKind.Utc), new DateTime(2024, 6, 1, 10, 0, 0, DateTimeKind.Utc))
        {
            Description = "Line1\nLine2; Extra",
            Timestamp = new DateTime(2024, 4, 2, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        builder.AddEvent(calendarEvent);
        var ics = builder.BuildString();

        // Assert
        Assert.Contains("SUMMARY:Summary\\, Test", ics);
        Assert.Contains("DESCRIPTION:Line1\\nLine2\\; Extra", ics);
    }

    [Fact]
    public void Build_EventWithTimezone_UsesLocalTimeNotation()
    {
        // Arrange
        var builder = _factory.Create();
        var start = new DateTime(2024, 7, 10, 9, 0, 0, DateTimeKind.Unspecified);
        var end = start.AddHours(2);
        var calendarEvent = new IcsCalendarEvent("3@sysjaky_n", "Local Event", start, end)
        {
            TimeZoneId = "Europe/Prague",
            Timestamp = new DateTime(2024, 4, 3, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        builder.AddEvent(calendarEvent);
        var ics = builder.BuildString();

        // Assert
        Assert.Contains("DTSTART;TZID=Europe/Prague:20240710T090000", ics);
        Assert.Contains("DTEND;TZID=Europe/Prague:20240710T110000", ics);
        Assert.DoesNotContain("Z", ics.Split('\n').First(line => line.StartsWith("DTSTART"))); // Ensure no UTC suffix
    }

    [Fact]
    public void BuildFile_ReturnsTypedFileResultWithCalendarContentType()
    {
        // Arrange
        var builder = _factory.Create("Calendar Name", includeGregorianCalendar: true);
        var calendarEvent = new IcsCalendarEvent("4@sysjaky_n", "With Name", new DateTime(2024, 8, 5, 12, 0, 0, DateTimeKind.Utc), new DateTime(2024, 8, 5, 13, 0, 0, DateTimeKind.Utc))
        {
            Timestamp = new DateTime(2024, 4, 4, 12, 0, 0, DateTimeKind.Utc)
        };
        builder.AddEvent(calendarEvent);

        // Act
        var result = builder.BuildFile("test.ics");

        // Assert
        var fileResult = Assert.IsType<FileContentHttpResult>(result);
        Assert.Equal("text/calendar", fileResult.ContentType);
        Assert.Equal("test.ics", fileResult.FileDownloadName);
        var content = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
        Assert.Contains("X-WR-CALNAME:Calendar Name", content);
        Assert.Contains("CALSCALE:GREGORIAN", content);
    }

    [Fact]
    public void AddEvent_EndBeforeStart_ThrowsArgumentException()
    {
        // Arrange
        var builder = _factory.Create();
        var calendarEvent = new IcsCalendarEvent("5@sysjaky_n", "Invalid", new DateTime(2024, 9, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2024, 9, 1, 9, 0, 0, DateTimeKind.Utc));

        // Act & Assert
        Assert.Throws<ArgumentException>(() => builder.AddEvent(calendarEvent));
    }
}
