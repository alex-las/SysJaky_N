using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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

        var builder = CreateBuilder();
        var client = new PohodaXmlClient(httpClient, Options.Create(options), builder, NullLogger<PohodaXmlClient>.Instance);
        var service = new PohodaExportService(client, builder, context, TimeProvider.System, Options.Create(options), new NoopAuditService(), CreateHostEnvironment(), NullLogger<PohodaExportService>.Instance);

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

        var client = new PohodaXmlClient(httpClient, Options.Create(options), CreateBuilder(), NullLogger<PohodaXmlClient>.Instance);

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

        var builder = CreateBuilder();
        var client = new PohodaXmlClient(httpClient, Options.Create(options), builder, NullLogger<PohodaXmlClient>.Instance);
        var service = new PohodaExportService(client, builder, context, TimeProvider.System, Options.Create(options), new NoopAuditService(), CreateHostEnvironment(), NullLogger<PohodaExportService>.Instance);

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

    [Fact]
    public async Task ExportOrderAsync_WhenDisabled_WritesPayloadToExportDirectory()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var exportDirectory = Path.Combine(Path.GetTempPath(), "SysJaky_N.Tests", Guid.NewGuid().ToString("N"));
        var options = new PohodaXmlOptions
        {
            Enabled = false,
            ExportDirectory = exportDirectory,
            Application = "SysJaky_N"
        };

        var context = CreateDbContext();
        var order = CreateOrder();
        context.Orders.Add(order);
        var job = new PohodaExportJob { OrderId = order.Id, Order = order };
        context.PohodaExportJobs.Add(job);
        await context.SaveChangesAsync();

        var builder = CreateBuilder();
        var client = new PohodaXmlClient(httpClient, Options.Create(options), builder, NullLogger<PohodaXmlClient>.Instance);
        var service = new PohodaExportService(client, builder, context, TimeProvider.System, Options.Create(options), new NoopAuditService(), CreateHostEnvironment(), NullLogger<PohodaExportService>.Instance);

        try
        {
            await service.ExportOrderAsync(job);

            Assert.Equal(PohodaExportJobStatus.Succeeded, job.Status);

            Assert.True(Directory.Exists(exportDirectory));
            var files = Directory.GetFiles(exportDirectory);
            var filePath = Assert.Single(files);

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding = Encoding.GetEncoding(options.EncodingName);
            var content = await File.ReadAllTextAsync(filePath, encoding);

            Assert.Contains($"Invoice-{order.Id}", content);
        }
        finally
        {
            if (Directory.Exists(exportDirectory))
            {
                Directory.Delete(exportDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExportOrderAsync_WhenDisabled_UsesContentRootTempDirectory()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var contentRoot = Path.Combine(Path.GetTempPath(), "SysJaky_N.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        var options = new PohodaXmlOptions
        {
            Enabled = false,
            ExportDirectory = "temp",
            Application = "SysJaky_N"
        };

        var context = CreateDbContext();
        var order = CreateOrder();
        context.Orders.Add(order);
        var job = new PohodaExportJob { OrderId = order.Id, Order = order };
        context.PohodaExportJobs.Add(job);
        await context.SaveChangesAsync();

        var builder = CreateBuilder();
        var client = new PohodaXmlClient(httpClient, Options.Create(options), builder, NullLogger<PohodaXmlClient>.Instance);
        var environment = CreateHostEnvironment(contentRoot);
        var service = new PohodaExportService(client, builder, context, TimeProvider.System, Options.Create(options), new NoopAuditService(), environment, NullLogger<PohodaExportService>.Instance);

        try
        {
            await service.ExportOrderAsync(job);

            Assert.Equal(PohodaExportJobStatus.Succeeded, job.Status);

            var exportDirectoryPath = Path.Combine(contentRoot, "temp");
            Assert.True(Directory.Exists(exportDirectoryPath));
            var files = Directory.GetFiles(exportDirectoryPath);
            var filePath = Assert.Single(files);

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding = Encoding.GetEncoding(options.EncodingName);
            var content = await File.ReadAllTextAsync(filePath, encoding);

            Assert.Contains($"Invoice-{order.Id}", content);
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveExportDirectory_WhenRootedPathStartsWithSeparator_UsesAbsolutePath()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var options = new PohodaXmlOptions
        {
            Enabled = false,
            ExportDirectory = "/pohoda-test-root",
            Application = "SysJaky_N"
        };

        var context = CreateDbContext();
        var builder = CreateBuilder();
        var client = new PohodaXmlClient(httpClient, Options.Create(options), builder, NullLogger<PohodaXmlClient>.Instance);
        var environment = CreateHostEnvironment();
        var service = new PohodaExportService(
            client,
            builder,
            context,
            TimeProvider.System,
            Options.Create(options),
            new NoopAuditService(),
            environment,
            NullLogger<PohodaExportService>.Instance);

        var method = typeof(PohodaExportService).GetMethod(
            "ResolveExportDirectory",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);

        var resolved = (string)method!.Invoke(service, null)!;

        Assert.Equal(Path.GetFullPath(options.ExportDirectory), resolved);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static PohodaXmlBuilder CreateBuilder()
        => new(PohodaXmlSchemaProvider.DefaultSchemas);

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

    private static IHostEnvironment CreateHostEnvironment(string? contentRoot = null)
        => new TestHostEnvironment
        {
            EnvironmentName = Environments.Development,
            ApplicationName = "SysJaky_N",
            ContentRootPath = contentRoot ?? Directory.GetCurrentDirectory()
        };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SysJaky_N";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
