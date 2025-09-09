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

namespace SysJaky_N.Pages.Orders;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly PaymentService _paymentService;

    public DetailsModel(ApplicationDbContext context, IConfiguration configuration, PaymentService paymentService)
    {
        _context = context;
        _configuration = configuration;
        _paymentService = paymentService;
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
