using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using QRCoder;
using System.Globalization;
using SysJaky_N.Services;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.IO;

namespace SysJaky_N.Pages.Orders;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly PaymentService _paymentService;
    private readonly IConverter _converter;
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;

    public DetailsModel(ApplicationDbContext context, IConfiguration configuration, PaymentService paymentService, IConverter converter, IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider)
    {
        _context = context;
        _configuration = configuration;
        _paymentService = paymentService;
        _converter = converter;
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
    }

    public Order Order { get; set; } = default!;
    public string? QrCodeImage { get; set; }
    public bool PaymentEnabled { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, string? session_id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Course)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!User.IsInRole("Admin") && order.UserId != userId)
            return Forbid();

        if (!string.IsNullOrEmpty(session_id))
        {
            await _paymentService.HandleSuccessAsync(session_id);
            await _context.Entry(order).ReloadAsync();
        }

        Order = order;
        PaymentEnabled = _paymentService.IsEnabled;

        var iban = _configuration["Payment:Iban"] ?? string.Empty;
        var vsPrefix = _configuration["Payment:VsPrefix"] ?? string.Empty;
        var vs = $"{vsPrefix}{order.Id}";
        var amount = order.TotalPrice.ToString("0.00", CultureInfo.InvariantCulture);
        var payload = $"SPD*1.0*ACC:{iban}*AM:{amount}*CC:CZK*X-VS:{vs}";

        var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        var bytes = qrCode.GetGraphic(20);
        QrCodeImage = $"data:image/png;base64,{Convert.ToBase64String(bytes)}";

        return Page();
    }

    public async Task<IActionResult> OnGetDownloadInvoiceAsync(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Course)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!User.IsInRole("Admin") && order.UserId != userId)
            return Forbid();

        var html = await RenderViewAsync("/Pages/Shared/Invoice.cshtml", order);

        var doc = new HtmlToPdfDocument
        {
            GlobalSettings = {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Portrait,
                PaperSize = PaperKind.A4
            },
            Objects = {
                new ObjectSettings {
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

    private async Task<string> RenderViewAsync<TModel>(string viewPath, TModel model)
    {
        var actionContext = new ActionContext(HttpContext, RouteData, PageContext.ActionDescriptor);
        using var sw = new StringWriter();
        var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: true);
        if (!viewResult.Success)
            throw new InvalidOperationException($"View '{viewPath}' not found.");

        var viewDictionary = new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model
        };
        var tempData = new TempDataDictionary(HttpContext, _tempDataProvider);
        var viewContext = new ViewContext(actionContext, viewResult.View, viewDictionary, tempData, sw, new HtmlHelperOptions());
        await viewResult.View.RenderAsync(viewContext);
        return sw.ToString();
    }

    public async Task<IActionResult> OnPostPayAsync(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Course)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!User.IsInRole("Admin") && order.UserId != userId)
            return Forbid();

        var baseUrl = Url.Page("/Orders/Details", null, new { id = order.Id }, Request.Scheme) ?? string.Empty;
        var url = await _paymentService.CreatePaymentAsync(order, baseUrl, baseUrl);
        if (url == null)
            return RedirectToPage(new { id });

        return Redirect(url);
    }
}
