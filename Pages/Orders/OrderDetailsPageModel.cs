using System;
using System.Globalization;
using System.IO;
using System.Security.Claims;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QRCoder;
using SysJaky_N.Authorization;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Orders;

public abstract class OrderDetailsPageModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly PaymentService _paymentService;
    private readonly IConverter _converter;
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;

    protected OrderDetailsPageModel(
        ApplicationDbContext context,
        IConfiguration configuration,
        PaymentService paymentService,
        IConverter converter,
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider)
    {
        _context = context;
        _configuration = configuration;
        _paymentService = paymentService;
        _converter = converter;
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
    }

    public Order Order { get; protected set; } = default!;
    public string? QrCodeImage { get; protected set; }
    public bool PaymentEnabled { get; protected set; }

    protected virtual bool RequireOrderOwnership => true;
    protected virtual string PaymentReturnPagePath => "/Orders/Details";

    public virtual async Task<IActionResult> OnGetAsync(int id, string? session_id)
    {
        var orderQuery = _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Course)
            .Include(o => o.Items)
                .ThenInclude(i => i.SeatTokens);

        var order = await orderQuery.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        var accessCheck = EnsureOrderAccess(order);
        if (accessCheck != null)
        {
            return accessCheck;
        }

        if (!string.IsNullOrEmpty(session_id))
        {
            await _paymentService.HandleSuccessAsync(session_id);
            order = await orderQuery.FirstOrDefaultAsync(o => o.Id == id) ?? order;
        }

        Order = order;
        PaymentEnabled = _paymentService.IsEnabled;

        var payload = CreatePaymentPayload(order);
        var bytes = GenerateQrCodeBytes(payload);
        QrCodeImage = $"data:image/png;base64,{Convert.ToBase64String(bytes)}";

        return Page();
    }

    public virtual async Task<IActionResult> OnGetDownloadInvoiceAsync(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Course)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        var accessCheck = EnsureOrderAccess(order);
        if (accessCheck != null)
        {
            return accessCheck;
        }

        var html = await RenderViewAsync("/Pages/Shared/Invoice.cshtml", order);

        var doc = new HtmlToPdfDocument
        {
            GlobalSettings =
            {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Portrait,
                PaperSize = PaperKind.A4
            },
            Objects =
            {
                new ObjectSettings
                {
                    HtmlContent = html,
                    WebSettings = { DefaultEncoding = "utf-8" }
                }
            }
        };

        var pdf = _converter.Convert(doc);

        var invoicesDir = Path.Combine("wwwroot", "invoices");
        Directory.CreateDirectory(invoicesDir);
        var fileName = $"invoice_{order.Id}.pdf";
        var filePath = Path.Combine(invoicesDir, fileName);
        System.IO.File.WriteAllBytes(filePath, pdf);
        order.InvoicePath = $"/invoices/{fileName}";
        await _context.SaveChangesAsync();

        return File(pdf, "application/pdf", fileName);
    }

    public virtual async Task<IActionResult> OnGetDownloadQrAsync(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        var accessCheck = EnsureOrderAccess(order);
        if (accessCheck != null)
        {
            return accessCheck;
        }

        var payload = CreatePaymentPayload(order);
        var bytes = GenerateQrCodeBytes(payload);

        return File(bytes, "image/png", $"qr_{order.Id}.png");
    }

    public virtual async Task<IActionResult> OnPostPayAsync(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Course)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        var accessCheck = EnsureOrderAccess(order);
        if (accessCheck != null)
        {
            return accessCheck;
        }

        var baseUrl = Url.Page(PaymentReturnPagePath, null, new { id = order.Id }, Request.Scheme) ?? string.Empty;
        var url = await _paymentService.CreatePaymentAsync(order, baseUrl, baseUrl);
        if (url == null)
        {
            return RedirectToPage(new { id });
        }

        return Redirect(url);
    }

    protected async Task<string> RenderViewAsync<TModel>(string viewPath, TModel model)
    {
        var actionContext = new ActionContext(HttpContext, RouteData, PageContext.ActionDescriptor);
        using var sw = new StringWriter();
        var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: true);
        if (!viewResult.Success)
        {
            throw new InvalidOperationException($"View '{viewPath}' not found.");
        }

        var viewDictionary = new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model
        };
        var tempData = new TempDataDictionary(HttpContext, _tempDataProvider);
        var viewContext = new ViewContext(actionContext, viewResult.View, viewDictionary, tempData, sw, new HtmlHelperOptions());
        await viewResult.View.RenderAsync(viewContext);
        return sw.ToString();
    }

    protected IActionResult? EnsureOrderAccess(Order order)
    {
        if (!RequireOrderOwnership)
        {
            return null;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole(ApplicationRoles.Admin) || order.UserId == userId)
        {
            return null;
        }

        return Forbid();
    }

    protected string CreatePaymentPayload(Order order)
    {
        var iban = _configuration["Payment:Iban"] ?? string.Empty;
        var vsPrefix = _configuration["Payment:VsPrefix"] ?? string.Empty;
        var vs = $"{vsPrefix}{order.Id}";
        var amount = order.Total.ToString("0.00", CultureInfo.InvariantCulture);

        return $"SPD*1.0*ACC:{iban}*AM:{amount}*CC:CZK*X-VS:{vs}";
    }

    protected byte[] GenerateQrCodeBytes(string payload)
    {
        var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(20);
    }

}

