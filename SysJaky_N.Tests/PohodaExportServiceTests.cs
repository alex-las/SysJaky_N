using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using SysJaky_N.Services.Pohoda;
using Xunit;
using Xunit.Abstractions;

namespace SysJaky_N.Tests;

public class PohodaExportServiceTests
{
    private readonly ITestOutputHelper _output;

    public PohodaExportServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ExportOrderAsync_SendsInvoiceAndStoresInvoiceNumber()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var options = new PohodaXmlOptions
        {
            BaseUrl = "https://pohoda.example.com",
            Username = "user",
            Password = "pass",
            Application = "SysJaky_N",
            TimeoutSeconds = 30
        };

        var context = CreateDbContext();
        var order = CreateOrder();
        context.Orders.Add(order);
        var job = new PohodaExportJob { OrderId = order.Id, Order = order };
        context.PohodaExportJobs.Add(job);
        await context.SaveChangesAsync();

        var client = new PohodaXmlClient(httpClient, Options.Create(options), NullLogger<PohodaXmlClient>.Instance);
        var mapper = new OrderToInvoiceMapper();
        var service = new PohodaExportService(client, context, TimeProvider.System, Options.Create(options), new NoopAuditService(), mapper, NullLogger<PohodaExportService>.Instance);

        await service.ExportOrderAsync(job);

        Assert.Equal(PohodaExportJobStatus.Succeeded, job.Status);
        Assert.Equal("INV-42", order.InvoiceNumber);
        Assert.Contains(handler.Requests, r => r.RequestUri!.AbsolutePath.EndsWith("/xml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateRequest_AddsBasicAuthorizationPrefix()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var options = new PohodaXmlOptions
        {
            BaseUrl = "https://pohoda.example.com",
            Username = "user",
            Password = "pass",
            Application = "SysJaky_N",
            TimeoutSeconds = 30
        };

        var client = new PohodaXmlClient(httpClient, Options.Create(options), NullLogger<PohodaXmlClient>.Instance);

        await client.CheckStatusAsync();

        var request = Assert.Single(handler.Requests);
        Assert.True(request.Headers.TryGetValues("STW-Authorization", out var values));
        var header = Assert.Single(values);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var expectedEncoding = Encoding.GetEncoding(options.EncodingName);
        var expectedCredentials = Convert.ToBase64String(expectedEncoding.GetBytes($"{options.Username}:{options.Password}"));
        Assert.Equal($"Basic {expectedCredentials}", header);
        _output.WriteLine("Authorization header: {0}", header);
    }

    [Fact]
    public async Task Worker_ProcessesPendingJobs()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var options = new PohodaXmlOptions
        {
            BaseUrl = "https://pohoda.example.com",
            Username = "user",
            Password = "pass",
            Application = "SysJaky_N",
            ExportWorkerBatchSize = 5,
            TimeoutSeconds = 30
        };

        var context = CreateDbContext();
        var order = CreateOrder();
        context.Orders.Add(order);
        context.PohodaExportJobs.Add(new PohodaExportJob { OrderId = order.Id, Order = order });
        await context.SaveChangesAsync();

        var client = new PohodaXmlClient(httpClient, Options.Create(options), NullLogger<PohodaXmlClient>.Instance);
        var mapper = new OrderToInvoiceMapper();
        var service = new PohodaExportService(client, context, TimeProvider.System, Options.Create(options), new NoopAuditService(), mapper, NullLogger<PohodaExportService>.Instance);

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton<ApplicationDbContext>(context);
        services.AddSingleton<IPohodaExportService>(service);

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var worker = new PohodaExportWorker(scopeFactory, NullLogger<PohodaExportWorker>.Instance, Options.Create(options), TimeProvider.System);
        var executeMethod = typeof(PohodaExportWorker).GetMethod("ExecuteInScopeAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(executeMethod);
        await (Task)executeMethod!.Invoke(worker, new object[] { provider, CancellationToken.None })!;

        var job = context.PohodaExportJobs.Single();
        Assert.Equal(PohodaExportJobStatus.Succeeded, job.Status);
        Assert.Equal("INV-42", order.InvoiceNumber);
        Assert.NotEmpty(handler.Requests);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Order CreateOrder()
    {
        var order = new Order
        {
            Id = 42,
            CreatedAt = DateTime.UtcNow,
            PriceExclVat = 300m,
            Vat = 63m,
            Total = 363m,
            TotalPrice = 363m,
            PaymentConfirmation = "CONF123"
        };

        order.Items.Add(new OrderItem
        {
            Id = 1,
            OrderId = 42,
            CourseId = 100,
            Course = new Course { Id = 100, Title = "Course A" },
            Quantity = 1,
            UnitPriceExclVat = 150m,
            Vat = 31.5m,
            Total = 181.5m
        });

        order.Items.Add(new OrderItem
        {
            Id = 2,
            OrderId = 42,
            CourseId = 200,
            Course = new Course { Id = 200, Title = "Course B" },
            Quantity = 2,
            UnitPriceExclVat = 75m,
            Vat = 31.5m,
            Total = 181.5m
        });

        return order;
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public IList<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }

            var content = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><rsp:responsePack xmlns:rsp=\"http://www.stormware.cz/schema/version_2/response.xsd\" state=\"ok\"><rsp:responsePackItem id=\"Invoice\" state=\"ok\"><rsp:invoiceResponse><inv:invoice xmlns:inv=\"http://www.stormware.cz/schema/version_2/invoice.xsd\"><inv:invoiceHeader><inv:number><typ:numberAssigned xmlns:typ=\"http://www.stormware.cz/schema/version_2/type.xsd\">INV-42</typ:numberAssigned></inv:number></inv:invoiceHeader></inv:invoice></rsp:invoiceResponse></rsp:responsePackItem></rsp:responsePack>";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            };

            return Task.FromResult(response);
        }
    }

    private sealed class NoopAuditService : IAuditService
    {
        public Task LogAsync(string? userId, string action, string? data = null) => Task.CompletedTask;
    }
}
