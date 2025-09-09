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

namespace SysJaky_N.Pages.Orders;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public DetailsModel(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public Order Order { get; set; } = default!;
    public string? QrCodeImage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
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

        Order = order;

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
}
