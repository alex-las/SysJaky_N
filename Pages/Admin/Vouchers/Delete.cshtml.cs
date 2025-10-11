using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Vouchers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<DeleteModel> _localizer;

    public DeleteModel(ApplicationDbContext context, IStringLocalizer<DeleteModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    [BindProperty]
    public Voucher Voucher { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var voucher = await _context.Vouchers
            .Include(v => v.AppliesToCourse)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (voucher == null)
        {
            return NotFound(_localizer["VoucherNotFound"]);
        }
        if (voucher.ExpiresUtc.HasValue)
        {
            voucher.ExpiresUtc = DateTime.SpecifyKind(voucher.ExpiresUtc.Value, DateTimeKind.Utc).ToLocalTime();
        }

        Voucher = voucher;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var voucher = await _context.Vouchers.FindAsync(Voucher.Id);
        if (voucher != null)
        {
            _context.Vouchers.Remove(voucher);
            await _context.SaveChangesAsync();
        }
        return RedirectToPage("Index");
    }
}
