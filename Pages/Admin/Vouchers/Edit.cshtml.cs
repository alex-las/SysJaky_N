using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Vouchers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<EditModel> _localizer;

    public EditModel(ApplicationDbContext context, IStringLocalizer<EditModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    [BindProperty]
    public Voucher Voucher { get; set; } = default!;

    public List<SelectListItem> Courses { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var voucher = await _context.Vouchers
            .AsNoTracking()
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
        await LoadCoursesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCoursesAsync();

        var voucherToUpdate = await _context.Vouchers
            .FirstOrDefaultAsync(v => v.Id == Voucher.Id);

        if (voucherToUpdate == null)
        {
            return NotFound(_localizer["VoucherNotFound"]);
        }

        Voucher.Code = Voucher.Code?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Voucher.Code))
        {
            ModelState.AddModelError("Voucher.Code", _localizer["ErrorCodeRequired"]);
        }
        else
        {
            Voucher.Code = Voucher.Code.ToUpperInvariant();
            bool exists = await _context.Vouchers
                .AnyAsync(v => v.Id != Voucher.Id && v.Code == Voucher.Code);
            if (exists)
            {
                ModelState.AddModelError("Voucher.Code", _localizer["ErrorCodeUnique"]);
            }
        }

        if (Voucher.Type == VoucherType.Percentage)
        {
            if (Voucher.Value <= 0 || Voucher.Value > 100)
            {
                ModelState.AddModelError("Voucher.Value", _localizer["ErrorPercentageRange"]);
            }
        }
        else if (Voucher.Value <= 0)
        {
            ModelState.AddModelError("Voucher.Value", _localizer["ErrorAmountPositive"]);
        }

        if (Voucher.MaxRedemptions.HasValue && Voucher.MaxRedemptions.Value < voucherToUpdate.UsedCount)
        {
            ModelState.AddModelError("Voucher.MaxRedemptions", _localizer["ErrorMaxRedemptions"]);
        }

        DateTime? expiresUtc = null;
        if (Voucher.ExpiresUtc.HasValue)
        {
            expiresUtc = DateTime.SpecifyKind(Voucher.ExpiresUtc.Value, DateTimeKind.Local).ToUniversalTime();
        }

        if (!ModelState.IsValid)
        {
            Voucher.UsedCount = voucherToUpdate.UsedCount;
            return Page();
        }

        voucherToUpdate.Code = Voucher.Code;
        voucherToUpdate.Type = Voucher.Type;
        voucherToUpdate.Value = Voucher.Value;
        voucherToUpdate.ExpiresUtc = expiresUtc;
        voucherToUpdate.MaxRedemptions = Voucher.MaxRedemptions;
        voucherToUpdate.AppliesToCourseId = Voucher.AppliesToCourseId;

        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task LoadCoursesAsync()
    {
        var courseItems = await _context.Courses
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title,
                Selected = Voucher.AppliesToCourseId == c.Id
            })
            .ToListAsync();

        Courses = new List<SelectListItem>
        {
            new SelectListItem(_localizer["AllCourses"], string.Empty, Voucher.AppliesToCourseId == null)
        };
        Courses.AddRange(courseItems);
    }
}
