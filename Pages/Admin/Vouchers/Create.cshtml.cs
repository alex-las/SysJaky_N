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
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<CreateModel> _localizer;

    public CreateModel(ApplicationDbContext context, IStringLocalizer<CreateModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    [BindProperty]
    public Voucher Voucher { get; set; } = new();

    public List<SelectListItem> Courses { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadCoursesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCoursesAsync();

        Voucher.Code = Voucher.Code?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Voucher.Code))
        {
            ModelState.AddModelError("Voucher.Code", _localizer["ErrorCodeRequired"]);
        }
        else
        {
            Voucher.Code = Voucher.Code.ToUpperInvariant();

            bool codeExists = await _context.Vouchers
                .AnyAsync(v => v.Code == Voucher.Code);
            if (codeExists)
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

        DateTime? expiresUtc = null;
        if (Voucher.ExpiresUtc.HasValue)
        {
            expiresUtc = DateTime.SpecifyKind(Voucher.ExpiresUtc.Value, DateTimeKind.Local).ToUniversalTime();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Voucher.ExpiresUtc = expiresUtc;

        _context.Vouchers.Add(Voucher);
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
