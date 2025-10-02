using System;
using System.Collections.Generic;
using SysJaky_N.Models;

namespace SysJaky_N.Extensions;

public static class OrderStatusExtensions
{
    public static string GetLocalizationKeySuffix(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Pending",
        OrderStatus.Paid => "Paid",
        OrderStatus.Cancelled => "Cancelled",
        OrderStatus.Refunded => "Refunded",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static IReadOnlyDictionary<OrderStatus, string> CreateTranslationMap(Func<OrderStatus, string> translationFactory)
    {
        ArgumentNullException.ThrowIfNull(translationFactory);

        var statuses = Enum.GetValues<OrderStatus>();
        var translations = new Dictionary<OrderStatus, string>(statuses.Length);

        foreach (var status in statuses)
        {
            translations[status] = translationFactory(status);
        }

        return translations;
    }
}
