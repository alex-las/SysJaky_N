using System;
using System.Collections.Generic;
using System.Globalization;
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
            TimeoutSeconds = 30,
            RetryCount = 0
        };

        var context = CreateDbContext();
        var order = CreateOrder();
        context.Orders.Add(order);
        var job = new PohodaExportJob { OrderId = order.Id, Order = order };
        context.PohodaExportJobs.Add(job);
        await context.SaveChangesAsync();

        var builder = CreateBuilder();
        var parser = new PohodaResponseParser();
        var client = new PohodaXmlClient(
            httpClient,
            Options.Create(options),
            builder,
            parser,
            CreateListBuilder(),
            CreateListParser(),
            NullLogger<PohodaXmlClient>.Instance,
            CreatePayloadSanitizer(),
            new PrometheusPohodaMetrics());
        var idempotencyStore = CreateIdempotencyStore(context);
        var service = new PohodaExportService(client, builder, context, TimeProvider.System, Options.Create(options), new NoopAuditService(), CreateHostEnvironment(), idempotencyStore, NullLogger<PohodaExportService>.Instance);

        await service.ExportOrderAsync(job);

        Assert.Equal(PohodaExportJobStatus.Succeeded, job.Status);
        Assert.Equal("INV-42", order.InvoiceNumber);
        Assert.Equal("INV-42", job.DocumentNumber);
        Assert.Equal("12345", job.DocumentId);
        Assert.Null(job.Warnings);
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
            TimeoutSeconds = 30,
            RetryCount = 0
        };

        var client = new PohodaXmlClient(
            httpClient,
            Options.Create(options),
            CreateBuilder(),
            new PohodaResponseParser(),
            CreateListBuilder(),
            CreateListParser(),
            NullLogger<PohodaXmlClient>.Instance,
            CreatePayloadSanitizer(),
            new PrometheusPohodaMetrics());

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
            TimeoutSeconds = 30,
            RetryCount = 0
        };

        var context = CreateDbContext();
        var order = CreateOrder();
        context.Orders.Add(order);
        context.PohodaExportJobs.Add(new PohodaExportJob { OrderId = order.Id, Order = order });
        await context.SaveChangesAsync();

        var builder = CreateBuilder();
        var client = new PohodaXmlClient(
            httpClient,
            Options.Create(options),
            builder,
            new PohodaResponseParser(),
            CreateListBuilder(),
            CreateListParser(),
            NullLogger<PohodaXmlClient>.Instance,
            CreatePayloadSanitizer(),
            new PrometheusPohodaMetrics());
        var clientOptions = (PohodaXmlOptions)typeof(PohodaXmlClient)
            .GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(client)!;
        Assert.Equal(0, clientOptions.RetryCount);
        var parser = CreateListParser();
        var parsed = parser.Parse(CreateListInvoiceResponse(order.Id, "INV-42"));
        Assert.Contains(parsed, status => status.SymVar == order.Id.ToString(CultureInfo.InvariantCulture));
        var filter = new PohodaListFilter
        {
            VariableSymbol = order.Id.ToString(CultureInfo.InvariantCulture),
            DateFrom = DateOnly.FromDateTime(order.CreatedAt),
            DateTo = DateOnly.FromDateTime(order.CreatedAt)
        };
        var listRequestTemplate = CreateListBuilder().Build(filter, options.Application);
        Assert.Contains("listInvoiceRequest", listRequestTemplate, StringComparison.OrdinalIgnoreCase);
        var idempotencyStore = CreateIdempotencyStore(context);
        var service = new PohodaExportService(client, builder, context, TimeProvider.System, Options.Create(options), new NoopAuditService(), CreateHostEnvironment(), idempotencyStore, NullLogger<PohodaExportService>.Instance);

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
        var client = new PohodaXmlClient(
            httpClient,
            Options.Create(options),
            builder,
            new PohodaResponseParser(),
            CreateListBuilder(),
            CreateListParser(),
            NullLogger<PohodaXmlClient>.Instance,
            CreatePayloadSanitizer(),
            new PrometheusPohodaMetrics());
        var idempotencyStore = CreateIdempotencyStore(context);
        var service = new PohodaExportService(client, builder, context, TimeProvider.System, Options.Create(options), new NoopAuditService(), CreateHostEnvironment(), idempotencyStore, NullLogger<PohodaExportService>.Instance);

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
    public async Task ExportOrderAsync_WhenTimeoutThenInvoiceExists_CompletesWithoutResend()
    {
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
        var client = new TestPohodaClient(order.Id);
        var idempotencyStore = CreateIdempotencyStore(context);
        var service = new PohodaExportService(
            client,
            builder,
            context,
            TimeProvider.System,
            Options.Create(options),
            new NoopAuditService(),
            CreateHostEnvironment(),
            idempotencyStore,
            NullLogger<PohodaExportService>.Instance);

        await service.QueueOrderAsync(order);

        await service.ExportOrderAsync(job);

        Assert.Equal(PohodaExportJobStatus.Pending, job.Status);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal(1, client.SendInvoiceCallCount);
        Assert.Equal(1, client.ListInvoiceCallCount);

        await service.ExportOrderAsync(job);

        Assert.Equal(2, job.AttemptCount);
        Assert.Equal(1, client.SendInvoiceCallCount);
        Assert.Equal(2, client.ListInvoiceCallCount);

        Assert.Equal(PohodaExportJobStatus.Succeeded, job.Status);
        Assert.Equal("INV-42", order.InvoiceNumber);
        Assert.Equal("INV-42", job.DocumentNumber);
        var record = context.PohodaIdempotencyRecords.Single();
        Assert.Equal(PohodaIdempotencyStatus.Succeeded, record.Status);
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
        var client = new PohodaXmlClient(
            httpClient,
            Options.Create(options),
            builder,
            new PohodaResponseParser(),
            CreateListBuilder(),
            CreateListParser(),
            NullLogger<PohodaXmlClient>.Instance,
            CreatePayloadSanitizer(),
            new PrometheusPohodaMetrics());
        var environment = CreateHostEnvironment(contentRoot);
        var idempotencyStore = CreateIdempotencyStore(context);
        var service = new PohodaExportService(client, builder, context, TimeProvider.System, Options.Create(options), new NoopAuditService(), environment, idempotencyStore, NullLogger<PohodaExportService>.Instance);

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
        var client = new PohodaXmlClient(
            httpClient,
            Options.Create(options),
            builder,
            new PohodaResponseParser(),
            CreateListBuilder(),
            CreateListParser(),
            NullLogger<PohodaXmlClient>.Instance,
            CreatePayloadSanitizer(),
            new PrometheusPohodaMetrics());
        var environment = CreateHostEnvironment();
        var service = new PohodaExportService(
            client,
            builder,
            context,
            TimeProvider.System,
            Options.Create(options),
            new NoopAuditService(),
            environment,
            CreateIdempotencyStore(context),
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

    private static PohodaListRequestBuilder CreateListBuilder()
        => new(PohodaXmlSchemaProvider.DefaultSchemas);

    private static PohodaListParser CreateListParser()
        => new();

    private static PohodaPayloadSanitizer CreatePayloadSanitizer()
    {
        var root = Path.Combine(Path.GetTempPath(), "SysJaky_N.Tests", "PohodaPayloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var environment = CreateHostEnvironment(root);
        return new PohodaPayloadSanitizer(environment, TimeProvider.System, NullLogger<PohodaPayloadSanitizer>.Instance);
    }

    private static IPohodaIdempotencyStore CreateIdempotencyStore(ApplicationDbContext context)
        => new PohodaIdempotencyStore(context, TimeProvider.System, NullLogger<PohodaIdempotencyStore>.Instance);

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

    private static string CreateListInvoiceResponse(int orderId, string invoiceNumber)
    {
        var symVar = orderId.ToString(CultureInfo.InvariantCulture);
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><rsp:responsePack xmlns:rsp=\"http://www.stormware.cz/schema/version_2/response.xsd\" state=\"ok\"><rsp:responsePackItem id=\"InvoiceList\" state=\"ok\"><lst:listInvoiceResponse xmlns:lst=\"http://www.stormware.cz/schema/version_2/list.xsd\"><lst:listInvoice><inv:invoice xmlns:inv=\"http://www.stormware.cz/schema/version_2/invoice.xsd\"><inv:invoiceHeader><inv:number><typ:numberAssigned xmlns:typ=\"http://www.stormware.cz/schema/version_2/type.xsd\">{invoiceNumber}</typ:numberAssigned></inv:number><inv:symVar>{symVar}</inv:symVar></inv:invoiceHeader></inv:invoice></lst:listInvoice></lst:listInvoiceResponse></rsp:responsePackItem></rsp:responsePack>";
    }

    private sealed class TestPohodaClient : IPohodaClient
    {
        private readonly string _invoiceNumber = "INV-42";
        private readonly string _symVar;

        public TestPohodaClient(int orderId)
        {
            _symVar = orderId.ToString(CultureInfo.InvariantCulture);
        }

        public int SendInvoiceCallCount { get; private set; }

        public int ListInvoiceCallCount { get; private set; }

        public Task<PohodaResponse> SendInvoiceAsync(
            string dataPack,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            SendInvoiceCallCount += 1;

            if (SendInvoiceCallCount == 1)
            {
                throw new TaskCanceledException("Simulated timeout.");
            }

            throw new InvalidOperationException("Invoice resend should not occur in this scenario.");
        }

        public Task<IReadOnlyCollection<InvoiceStatus>> ListInvoicesAsync(
            PohodaListFilter filter,
            CancellationToken cancellationToken = default)
        {
            ListInvoiceCallCount += 1;

            if (ListInvoiceCallCount == 1)
            {
                return Task.FromResult<IReadOnlyCollection<InvoiceStatus>>(Array.Empty<InvoiceStatus>());
            }

            var status = new InvoiceStatus(
                _invoiceNumber,
                _symVar,
                0m,
                true,
                null,
                null);

            return Task.FromResult<IReadOnlyCollection<InvoiceStatus>>(new[] { status });
        }

        public Task<bool> CheckStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public IList<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();
        public IList<string> Payloads { get; } = new List<string>();

        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? OnSendAsync { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (request.Content is not null)
            {
                var payload = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                Payloads.Add(payload);
            }

            if (OnSendAsync is not null)
            {
                return await OnSendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            var content = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><rsp:responsePack xmlns:rsp=\"http://www.stormware.cz/schema/version_2/response.xsd\" state=\"ok\"><rsp:responsePackItem id=\"Invoice\" state=\"ok\" documentNumber=\"INV-42\" documentId=\"12345\"><rsp:invoiceResponse><inv:invoice xmlns:inv=\"http://www.stormware.cz/schema/version_2/invoice.xsd\"><inv:invoiceHeader><inv:number><typ:numberAssigned xmlns:typ=\"http://www.stormware.cz/schema/version_2/type.xsd\">INV-42</typ:numberAssigned></inv:number></inv:invoiceHeader></inv:invoice></rsp:invoiceResponse></rsp:responsePackItem></rsp:responsePack>";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            };

            return response;
        }
    }

    private sealed class NoopAuditService : IAuditService
    {
        public Task LogAsync(string? userId, string action, string? data = null) => Task.CompletedTask;
    }

    private static IHostEnvironment CreateHostEnvironment(string? contentRoot = null)
    {
        var root = contentRoot ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(root);
        return new TestHostEnvironment
        {
            EnvironmentName = Environments.Development,
            ApplicationName = "SysJaky_N",
            ContentRootPath = root
        };
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SysJaky_N";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
