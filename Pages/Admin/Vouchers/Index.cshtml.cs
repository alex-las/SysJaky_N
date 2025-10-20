using System;
using System.Collections.Generic;
using System.Linq;
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
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<IndexModel> _localizer;

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public IList<Voucher> Vouchers { get; set; } = new List<Voucher>();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public VoucherType? Type { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CourseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public VoucherStateFilter? State { get; set; }

    [BindProperty(SupportsGet = true)]
    public new int Page { get; set; } = 1;

    public const int PageSize = 20;

    public int TotalPages { get; private set; }

    public VoucherStatistics Statistics { get; private set; } = VoucherStatistics.Empty;

    public List<SelectListItem> CourseOptions { get; private set; } = new();

    public List<SelectListItem> TypeOptions { get; private set; } = new();

    public List<SelectListItem> StateOptions { get; private set; } = new();

    public async Task OnGetAsync()
    {
        ViewData["Title"] = _localizer["Title"];

        var query = _context.Vouchers
            .AsNoTracking()
            .Include(v => v.AppliesToCourse)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var pattern = $"%{Search.Trim()}%";
            query = query.Where(v => EF.Functions.Like(v.Code, pattern));
        }

        if (Type.HasValue)
        {
            query = query.Where(v => v.Type == Type.Value);
        }

        if (CourseId.HasValue)
        {
            query = query.Where(v => v.AppliesToCourseId == CourseId.Value);
        }

        await PopulateCourseOptionsAsync();
        BuildTypeOptions();
        BuildStateOptions();

        var now = DateTime.UtcNow;

        var totalCount = await query.CountAsync();
        var activeCount = await query.CountAsync(v =>
            (!v.ExpiresUtc.HasValue || v.ExpiresUtc > now) &&
            (!v.MaxRedemptions.HasValue || v.UsedCount < v.MaxRedemptions.Value));
        var expiredCount = await query.CountAsync(v => v.ExpiresUtc.HasValue && v.ExpiresUtc <= now);
        var depletedCount = await query.CountAsync(v => v.MaxRedemptions.HasValue && v.UsedCount >= v.MaxRedemptions.Value);
        var availableCodes = await query.SumAsync(v => v.MaxRedemptions.HasValue && v.MaxRedemptions.Value > v.UsedCount
            ? v.MaxRedemptions.Value - v.UsedCount
            : 0);

        Statistics = new VoucherStatistics(totalCount, activeCount, expiredCount, depletedCount, availableCodes);

        if (State.HasValue)
        {
            query = State.Value switch
            {
                VoucherStateFilter.Active => query.Where(v =>
                    (!v.ExpiresUtc.HasValue || v.ExpiresUtc > now) &&
                    (!v.MaxRedemptions.HasValue || v.UsedCount < v.MaxRedemptions.Value)),
                VoucherStateFilter.Expired => query.Where(v => v.ExpiresUtc.HasValue && v.ExpiresUtc <= now),
                VoucherStateFilter.Depleted => query.Where(v => v.MaxRedemptions.HasValue && v.UsedCount >= v.MaxRedemptions.Value),
                _ => query
            };
        }

        var filteredCount = await query.CountAsync();
        TotalPages = filteredCount == 0 ? 0 : (int)Math.Ceiling(filteredCount / (double)PageSize);

        if (Page < 1)
        {
            Page = 1;
        }

        if (TotalPages > 0 && Page > TotalPages)
        {
            Page = TotalPages;
        }

        if (TotalPages == 0)
        {
            Page = 1;
        }

        Vouchers = await query
            .OrderBy(v => v.Code)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    private async Task PopulateCourseOptionsAsync()
    {
        var courses = await _context.Courses
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .Select(c => new SelectListItem(c.Title, c.Id.ToString(), CourseId == c.Id))
            .ToListAsync();

        CourseOptions = new List<SelectListItem>
        {
            new SelectListItem(_localizer["CourseAllOption"], string.Empty, CourseId is null)
        };

        CourseOptions.AddRange(courses);
    }

    private void BuildTypeOptions()
    {
        TypeOptions = new List<SelectListItem>
        {
            new SelectListItem(_localizer["TypeAllOption"], string.Empty, Type is null),
            new SelectListItem(_localizer["TypePercentageOption"], VoucherType.Percentage.ToString(), Type == VoucherType.Percentage),
            new SelectListItem(_localizer["TypeFixedAmountOption"], VoucherType.FixedAmount.ToString(), Type == VoucherType.FixedAmount)
        };
    }

    private void BuildStateOptions()
    {
        StateOptions = new List<SelectListItem>
        {
            new SelectListItem(_localizer["StateAllOption"], string.Empty, State is null),
            new SelectListItem(_localizer["StateActiveOption"], VoucherStateFilter.Active.ToString(), State == VoucherStateFilter.Active),
            new SelectListItem(_localizer["StateExpiredOption"], VoucherStateFilter.Expired.ToString(), State == VoucherStateFilter.Expired),
            new SelectListItem(_localizer["StateDepletedOption"], VoucherStateFilter.Depleted.ToString(), State == VoucherStateFilter.Depleted)
        };
    }

    public enum VoucherStateFilter
    {
        Active,
        Expired,
        Depleted
    }

    public record struct VoucherStatistics(int Total, int Active, int Expired, int Depleted, int AvailableCodes)
    {
        public static VoucherStatistics Empty => new(0, 0, 0, 0, 0);
    }
}
