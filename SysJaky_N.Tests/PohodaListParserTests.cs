using System;
using System.IO;
using SysJaky_N.Services.Pohoda;
using Xunit;

namespace SysJaky_N.Tests;

public class PohodaListParserTests
{
    [Fact]
    public void Parse_ReturnsStatusesWithValues()
    {
        var parser = new PohodaListParser();
        var samplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "SampleListInvoiceResponse.xml");
        var xml = File.ReadAllText(samplePath);

        var statuses = parser.Parse(xml);

        Assert.Equal(2, statuses.Count);

        var first = Assert.Contains(statuses, status => status.Number == "INV-123");
        Assert.Equal("42", first.SymVar);
        Assert.Equal(1500.00m, first.Total);
        Assert.True(first.Paid);
        Assert.Equal(new DateOnly(2024, 1, 31), first.DueDate);
        Assert.Equal(new DateOnly(2024, 2, 2), first.PaidAt);

        var second = Assert.Contains(statuses, status => status.Number == "INV-124");
        Assert.Equal("43", second.SymVar);
        Assert.Equal(2500.50m, second.Total);
        Assert.False(second.Paid);
        Assert.Equal(new DateOnly(2024, 2, 15), second.DueDate);
        Assert.Null(second.PaidAt);
    }
}
