using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Xml.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SysJaky_N.Services.Pohoda;

namespace SysJaky_N.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IPohodaClient _pohodaClient;
    private readonly PohodaXmlBuilder _xmlBuilder;
    private readonly PohodaXmlOptions _options;

    public InvoicesController(
        IPohodaClient pohodaClient,
        PohodaXmlBuilder xmlBuilder,
        PohodaXmlOptions options)
    {
        _pohodaClient = pohodaClient ?? throw new ArgumentNullException(nameof(pohodaClient));
        _xmlBuilder = xmlBuilder ?? throw new ArgumentNullException(nameof(xmlBuilder));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    [HttpPost]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var invoice = MapInvoice(request);
            var xml = _xmlBuilder.BuildIssuedInvoiceXml(invoice, _options.Application);
            var correlationId = $"pohoda-api-{Guid.NewGuid():N}";
            var response = await _pohodaClient
                .SendInvoiceAsync(xml, correlationId, cancellationToken)
                .ConfigureAwait(false);

            return Ok(new InvoiceCreatedResponse(
                response.State,
                response.DocumentNumber,
                response.DocumentId,
                response.Warnings,
                response.Errors));
        }
        catch (Exception ex)
        {
            if (TryMapException(ex, out var mapped))
            {
                return mapped;
            }

            throw;
        }
    }

    [HttpGet("{idOrNumber}")]
    public async Task<IActionResult> GetInvoiceStatus(string idOrNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idOrNumber))
        {
            return CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid identifier",
                "Invoice identifier must be provided.");
        }

        var trimmed = idOrNumber.Trim();

        try
        {
            var filter = new PohodaListFilter
            {
                Number = trimmed,
                VariableSymbol = trimmed
            };

            var invoices = await _pohodaClient
                .ListInvoicesAsync(filter, cancellationToken)
                .ConfigureAwait(false);

            if (invoices.Count == 0)
            {
                return NotFound();
            }

            return Ok(new InvoiceStatusResponse(trimmed, invoices.ToArray()));
        }
        catch (Exception ex)
        {
            if (TryMapException(ex, out var mapped))
            {
                return mapped;
            }

            throw;
        }
    }

    private bool TryMapException(Exception exception, out IActionResult result)
    {
        switch (exception)
        {
            case ValidationException validationException:
                result = CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invoice validation failed",
                    validationException.Message);
                return true;
            case XmlSchemaValidationException schemaException:
                result = CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invoice validation failed",
                    schemaException.Message);
                return true;
            case PohodaXmlException pohodaException:
                result = CreateProblem(
                    StatusCodes.Status502BadGateway,
                    "Pohoda service error",
                    pohodaException.Message,
                    problem =>
                    {
                        if (pohodaException.PayloadLog?.HasData == true)
                        {
                            problem.Extensions["payloadLog"] = new
                            {
                                pohodaException.PayloadLog.RequestPath,
                                pohodaException.PayloadLog.ResponsePath
                            };
                        }
                    });
                return true;
            default:
                result = default!;
                return false;
        }
    }

    private ObjectResult CreateProblem(
        int statusCode,
        string title,
        string detail,
        Action<ProblemDetails>? configure = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        configure?.Invoke(problem);

        return StatusCode(statusCode, problem);
    }

    private static InvoiceDto MapInvoice(CreateInvoiceRequest request)
    {
        if (request.Header is null)
        {
            throw new ValidationException("Invoice header is required.");
        }

        if (request.Summary is null)
        {
            throw new ValidationException("Invoice summary is required.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new ValidationException("Invoice must contain at least one item.");
        }

        var customer = request.Header.Customer is null
            ? null
            : new CustomerIdentity(
                request.Header.Customer.Company,
                request.Header.Customer.Name,
                request.Header.Customer.Street,
                request.Header.Customer.City,
                request.Header.Customer.Zip,
                request.Header.Customer.Country);

        var header = new InvoiceHeader(
            request.Header.InvoiceType,
            request.Header.OrderNumber,
            request.Header.Text,
            request.Header.Date,
            request.Header.TaxDate,
            request.Header.DueDate,
            request.Header.VariableSymbol,
            request.Header.SpecificSymbol,
            customer,
            request.Header.Note);

        var items = request.Items
            .Select(item => new InvoiceItem(
                item.Name,
                item.Quantity,
                item.UnitPriceExclVat,
                item.TotalExclVat,
                item.VatAmount,
                item.TotalInclVat,
                item.Discount,
                item.Rate))
            .ToList();

        return InvoiceDto.Create(
            header,
            items,
            request.Summary.TotalExclVat,
            request.Summary.TotalVat,
            request.Summary.TotalInclVat,
            request.Summary.NoneRateBase,
            request.Summary.LowRateBase,
            request.Summary.LowRateVat,
            request.Summary.HighRateBase,
            request.Summary.HighRateVat);
    }

    public sealed record InvoiceCreatedResponse(
        string State,
        string? DocumentNumber,
        string? DocumentId,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<string> Errors);

    public sealed record InvoiceStatusResponse(
        string Query,
        IReadOnlyCollection<InvoiceStatus> Invoices);

    public sealed class CreateInvoiceRequest
    {
        [Required]
        public InvoiceHeaderDto? Header { get; init; }
            = default!;

        [Required]
        [MinLength(1)]
        public List<InvoiceItemDto>? Items { get; init; }
            = new();

        [Required]
        public VatSummaryDto? Summary { get; init; }
            = default!;
    }

    public sealed class InvoiceHeaderDto
    {
        [Required]
        [StringLength(64)]
        public string InvoiceType { get; init; } = string.Empty;

        [Required]
        [StringLength(64)]
        public string OrderNumber { get; init; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string Text { get; init; } = string.Empty;

        [DataType(DataType.Date)]
        public DateOnly Date { get; init; }
            = DateOnly.FromDateTime(DateTime.Today);

        [DataType(DataType.Date)]
        public DateOnly TaxDate { get; init; }
            = DateOnly.FromDateTime(DateTime.Today);

        [DataType(DataType.Date)]
        public DateOnly DueDate { get; init; }
            = DateOnly.FromDateTime(DateTime.Today);

        [Required]
        [StringLength(32)]
        public string VariableSymbol { get; init; } = string.Empty;

        [StringLength(32)]
        public string? SpecificSymbol { get; init; }
            = null;

        public CustomerIdentityDto? Customer { get; init; }
            = null;

        [StringLength(512)]
        public string? Note { get; init; }
            = null;
    }

    public sealed class CustomerIdentityDto
    {
        [StringLength(255)]
        public string? Company { get; init; }
            = null;

        [StringLength(255)]
        public string? Name { get; init; }
            = null;

        [StringLength(255)]
        public string? Street { get; init; }
            = null;

        [StringLength(255)]
        public string? City { get; init; }
            = null;

        [StringLength(32)]
        public string? Zip { get; init; }
            = null;

        [StringLength(64)]
        public string? Country { get; init; }
            = null;
    }

    public sealed class InvoiceItemDto
    {
        [Required]
        [StringLength(255)]
        public string Name { get; init; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int Quantity { get; init; }
            = 1;

        public decimal UnitPriceExclVat { get; init; }
            = 0m;

        public decimal TotalExclVat { get; init; }
            = 0m;

        public decimal VatAmount { get; init; }
            = 0m;

        public decimal TotalInclVat { get; init; }
            = 0m;

        public decimal Discount { get; init; }
            = 0m;

        [Required]
        public VatRate Rate { get; init; }
            = VatRate.High;
    }

    public sealed class VatSummaryDto
    {
        public decimal TotalExclVat { get; init; }
            = 0m;

        public decimal TotalVat { get; init; }
            = 0m;

        public decimal TotalInclVat { get; init; }
            = 0m;

        public decimal? NoneRateBase { get; init; }
            = null;

        public decimal? LowRateBase { get; init; }
            = null;

        public decimal? LowRateVat { get; init; }
            = null;

        public decimal? HighRateBase { get; init; }
            = null;

        public decimal? HighRateVat { get; init; }
            = null;
    }
}
