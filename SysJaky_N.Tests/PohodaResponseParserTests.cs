using System;
using SysJaky_N.Services.Pohoda;
using Xunit;

namespace SysJaky_N.Tests;

public class PohodaResponseParserTests
{
    [Fact]
    public void Parse_ReturnsDocumentInfo_ForOkResponse()
    {
        const string xml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<rsp:responsePack xmlns:rsp=\"http://www.stormware.cz/schema/version_2/response.xsd\" state=\"ok\">" +
            "<rsp:responsePackItem id=\"Invoice\" state=\"ok\" documentNumber=\"INV-42\" documentId=\"12345\">" +
            "<rsp:invoiceResponse><inv:invoice xmlns:inv=\"http://www.stormware.cz/schema/version_2/invoice.xsd\">" +
            "<inv:invoiceHeader><inv:number><typ:numberAssigned xmlns:typ=\"http://www.stormware.cz/schema/version_2/type.xsd\">INV-42</typ:numberAssigned></inv:number></inv:invoiceHeader>" +
            "</inv:invoice></rsp:invoiceResponse></rsp:responsePackItem></rsp:responsePack>";

        var parser = new PohodaResponseParser();

        var response = parser.Parse(xml);

        Assert.Equal("ok", response.State);
        Assert.Equal("INV-42", response.DocumentNumber);
        Assert.Equal("12345", response.DocumentId);
        Assert.Empty(response.Warnings);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public void Parse_ReturnsWarnings_ForDuplicate()
    {
        const string xml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<rsp:responsePack xmlns:rsp=\"http://www.stormware.cz/schema/version_2/response.xsd\" state=\"ok\" stateInfo=\"Processed with warnings\">" +
            "<rsp:responsePackItem id=\"Invoice\" state=\"warning\" note=\"Invoice already exists\" documentNumber=\"INV-42\" documentId=\"12345\">" +
            "<rsp:warning code=\"105\" message=\"Duplicitní doklad\" />" +
            "</rsp:responsePackItem></rsp:responsePack>";

        var parser = new PohodaResponseParser();

        var response = parser.Parse(xml);

        Assert.Equal("warning", response.State);
        Assert.Equal("INV-42", response.DocumentNumber);
        Assert.Equal("12345", response.DocumentId);
        Assert.Contains(response.Warnings, warning => warning.Contains("Duplicitní", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.Warnings, warning => warning.Contains("exists", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(response.Errors);
    }

    [Fact]
    public void Parse_ReturnsErrors_ForValidationFailure()
    {
        const string xml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<rsp:responsePack xmlns:rsp=\"http://www.stormware.cz/schema/version_2/response.xsd\" state=\"error\" stateDetail=\"Validation failed\">" +
            "<rsp:responsePackItem id=\"Invoice\" state=\"error\">" +
            "<rsp:message>Chyba: Povinné pole není vyplněno.</rsp:message>" +
            "</rsp:responsePackItem></rsp:responsePack>";

        var parser = new PohodaResponseParser();

        var response = parser.Parse(xml);

        Assert.Equal("error", response.State);
        Assert.Null(response.DocumentNumber);
        Assert.Null(response.DocumentId);
        Assert.Empty(response.Warnings);
        Assert.Contains(response.Errors, error => error.Contains("Povinné", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.Errors, error => error.Contains("Validation failed", StringComparison.OrdinalIgnoreCase));
    }
}
