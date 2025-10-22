using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SysJaky_N.Controllers;
using SysJaky_N.Services.Pohoda;

namespace SysJaky_N.Tests;

public class InvoiceApiTests
{
    [Fact]
    public async Task PostInvoices_ReturnsDocumentInfo()
    {
        using var factory = new TestWebApplicationFactory();
        var pohodaClient = factory.PohodaClientMock;
        pohodaClient.Reset();

        pohodaClient
            .Setup(client => client.SendInvoiceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PohodaResponse(
                "ok",
                "INV-001",
                "12345",
                Array.Empty<string>(),
                Array.Empty<string>()));

        using var client = CreateHttpClient(factory);
        var response = await client.PostAsJsonAsync("/api/invoices", CreateInvoiceRequest());

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<InvoicesController.InvoiceCreatedResponse>();
        Assert.NotNull(payload);
        Assert.Equal("INV-001", payload.DocumentNumber);
        Assert.Equal("12345", payload.DocumentId);

        pohodaClient.Verify(client => client.SendInvoiceAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PostInvoices_ReturnsBadGatewayWhenPohodaFails()
    {
        using var factory = new TestWebApplicationFactory();
        var pohodaClient = factory.PohodaClientMock;
        pohodaClient.Reset();

        pohodaClient
            .Setup(client => client.SendInvoiceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PohodaXmlException(
                "Remote failure",
                payloadLog: new PohodaPayloadLog("request.xml", "response.xml")));

        using var client = CreateHttpClient(factory);
        var response = await client.PostAsJsonAsync("/api/invoices", CreateInvoiceRequest());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Pohoda service error", problem.Title);
        Assert.True(problem.Extensions.ContainsKey("payloadLog"));

        pohodaClient.Verify(client => client.SendInvoiceAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInvoices_ReturnsStatuses()
    {
        using var factory = new TestWebApplicationFactory();
        var pohodaClient = factory.PohodaClientMock;
        pohodaClient.Reset();

        var status = new InvoiceStatus(
            "2024-0001",
            "1001",
            121m,
            true,
            DateOnly.FromDateTime(DateTime.Today.AddDays(14)),
            DateOnly.FromDateTime(DateTime.Today));

        pohodaClient
            .Setup(client => client.ListInvoicesAsync(
                It.IsAny<PohodaListFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { status });

        using var client = CreateHttpClient(factory);
        var response = await client.GetAsync("/api/invoices/2024-0001");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<InvoicesController.InvoiceStatusResponse>();
        Assert.NotNull(payload);
        Assert.Equal("2024-0001", payload.Query);
        Assert.Single(payload.Invoices);
        Assert.Equal(status.Number, payload.Invoices.First().Number);

        pohodaClient.Verify(client => client.ListInvoicesAsync(
            It.IsAny<PohodaListFilter>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInvoices_ReturnsNotFoundWhenMissing()
    {
        using var factory = new TestWebApplicationFactory();
        var pohodaClient = factory.PohodaClientMock;
        pohodaClient.Reset();

        pohodaClient
            .Setup(client => client.ListInvoicesAsync(
                It.IsAny<PohodaListFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<InvoiceStatus>());

        using var client = CreateHttpClient(factory);
        var response = await client.GetAsync("/api/invoices/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        pohodaClient.Verify(client => client.ListInvoicesAsync(
            It.IsAny<PohodaListFilter>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HealthCheck_ReturnsOkWhenHealthy()
    {
        using var factory = new TestWebApplicationFactory();
        var pohodaClient = factory.PohodaClientMock;
        pohodaClient.Reset();

        pohodaClient
            .Setup(client => client.CheckStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        using var client = CreateHttpClient(factory);
        var response = await client.GetAsync("/health/pohoda");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        pohodaClient.Verify(client => client.CheckStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HealthCheck_ReturnsServiceUnavailableWhenUnhealthy()
    {
        using var factory = new TestWebApplicationFactory();
        var pohodaClient = factory.PohodaClientMock;
        pohodaClient.Reset();

        pohodaClient
            .Setup(client => client.CheckStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        using var client = CreateHttpClient(factory);
        var response = await client.GetAsync("/health/pohoda");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        pohodaClient.Verify(client => client.CheckStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static HttpClient CreateHttpClient(TestWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.AcceptEncoding.Clear();
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("identity");
        return client;
    }

    private static InvoicesController.CreateInvoiceRequest CreateInvoiceRequest()
    {
        return new InvoicesController.CreateInvoiceRequest
        {
            Header = new InvoicesController.InvoiceHeaderDto
            {
                InvoiceType = "issuedInvoice",
                OrderNumber = "ORDER-1",
                Text = "Test invoice",
                Date = DateOnly.FromDateTime(DateTime.Today),
                TaxDate = DateOnly.FromDateTime(DateTime.Today),
                DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14)),
                VariableSymbol = "1234567890",
                Customer = new InvoicesController.CustomerIdentityDto
                {
                    Company = "ACME",
                    Name = "John Doe",
                    Street = "Main Street 1",
                    City = "Prague",
                    Zip = "11000",
                    Country = "CZ"
                },
                Note = "Test note"
            },
            Items = new List<InvoicesController.InvoiceItemDto>
            {
                new()
                {
                    Name = "Course",
                    Quantity = 1,
                    UnitPriceExclVat = 100m,
                    TotalExclVat = 100m,
                    VatAmount = 21m,
                    TotalInclVat = 121m,
                    Discount = 0m,
                    Rate = VatRate.High
                }
            },
            Summary = new InvoicesController.VatSummaryDto
            {
                TotalExclVat = 100m,
                TotalVat = 21m,
                TotalInclVat = 121m,
                HighRateBase = 100m,
                HighRateVat = 21m
            }
        };
    }
}
