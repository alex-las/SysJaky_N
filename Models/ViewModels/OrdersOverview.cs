using System.Collections.Generic;
using SysJaky_N.Models;

namespace SysJaky_N.Models.ViewModels;

public class OrdersOverview
{
    public int TotalOrders { get; init; }

    public decimal TotalRevenue { get; init; }

    public IReadOnlyList<OrderStatusSummary> StatusSummaries { get; init; }
        = new List<OrderStatusSummary>();
}

public class OrderStatusSummary
{
    public required OrderStatus Status { get; init; }

    public required string Name { get; init; }

    public int Count { get; init; }

    public string Value => Status.ToString();
}
