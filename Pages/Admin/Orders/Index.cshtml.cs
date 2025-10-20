using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Extensions;
using SysJaky_N.Models;
using SysJaky_N.Models.ViewModels;

namespace SysJaky_N.Pages.Admin.Orders;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<IndexModel> _localizer;

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public IList<Order> Orders { get; set; } = new List<Order>();

    public OrdersOverview Overview { get; private set; } = new();

    public int TotalPages { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    public async Task OnGetAsync()
    {
        ViewData["Title"] = _localizer["Title"];

        var ordersQuery = _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            var likeTerm = $"%{term}%";

            if (int.TryParse(term, out var parsedId))
            {
                ordersQuery = ordersQuery.Where(o =>
                    o.Id == parsedId ||
                    (o.Customer != null && o.Customer.Email != null && EF.Functions.Like(o.Customer.Email, likeTerm)));
            }
            else
            {
                ordersQuery = ordersQuery.Where(o =>
                    o.Customer != null && o.Customer.Email != null && EF.Functions.Like(o.Customer.Email, likeTerm));
            }
        }

        if (!string.IsNullOrWhiteSpace(Status) && Enum.TryParse(Status, true, out OrderStatus parsedStatus))
        {
            ordersQuery = ordersQuery.Where(o => o.Status == parsedStatus);
        }

        if (From.HasValue)
        {
            var fromDate = From.Value.Date;
            ordersQuery = ordersQuery.Where(o => o.CreatedAt >= fromDate);
        }

        if (To.HasValue)
        {
            var toDate = To.Value.Date.AddDays(1);
            ordersQuery = ordersQuery.Where(o => o.CreatedAt < toDate);
        }

        var filteredQuery = ordersQuery;

        var groupedData = await filteredQuery
            .GroupBy(o => o.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count(),
                Revenue = g.Sum(o => o.Total)
            })
            .ToListAsync();

        var totalOrders = groupedData.Sum(item => item.Count);
        var totalRevenue = groupedData.Sum(item => item.Revenue);

        var statusSummaries = new List<OrderStatusSummary>();
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            var key = status.GetLocalizationKeySuffix();
            var count = groupedData.FirstOrDefault(item => item.Status == status)?.Count ?? 0;

            statusSummaries.Add(new OrderStatusSummary
            {
                Status = status,
                Name = _localizer[$"OrderStatus{key}"],
                Count = count
            });
        }

        Overview = new OrdersOverview
        {
            TotalOrders = totalOrders,
            TotalRevenue = totalRevenue,
            StatusSummaries = statusSummaries
        };

        if (PageIndex < 1)
        {
            PageIndex = 1;
        }

        TotalPages = totalOrders == 0
            ? 0
            : (int)Math.Ceiling(totalOrders / (double)PageSize);

        if (TotalPages == 0)
        {
            PageIndex = 1;
        }
        else if (PageIndex > TotalPages)
        {
            PageIndex = TotalPages;
        }

        var skip = (PageIndex - 1) * PageSize;
        if (skip < 0)
        {
            skip = 0;
        }

        Orders = await filteredQuery
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Take(PageSize)
            .ToListAsync();
    }
}

