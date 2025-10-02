using System;
using System.Collections.Generic;
using SysJaky_N.Extensions;
using SysJaky_N.Models;

namespace SysJaky_N.Models.ViewModels;

public class OrderDetailsViewModel
{
    public required Order Order { get; init; }
    public bool PaymentEnabled { get; init; }
    public string? QrCodeImage { get; init; }

    public string StatusLabel { get; init; } = string.Empty;
    public string DateLabel { get; init; } = string.Empty;

    public string CourseHeader { get; init; } = string.Empty;
    public string QuantityHeader { get; init; } = string.Empty;
    public string UnitPriceExclVatHeader { get; init; } = string.Empty;
    public string VatHeader { get; init; } = string.Empty;
    public string TotalHeader { get; init; } = string.Empty;

    public string SubtotalLabel { get; init; } = string.Empty;
    public string VatLabel { get; init; } = string.Empty;
    public string TotalLabel { get; init; } = string.Empty;

    public IReadOnlyDictionary<OrderStatus, string> StatusTranslations { get; init; }
        = OrderStatusExtensions.CreateTranslationMap(static _ => string.Empty);

    public string SeatTokensHeading { get; init; } = string.Empty;
    public string CourseFallbackFormat { get; init; } = "Course {0}";
    public string SeatTokenRedeemedFormat { get; init; } = "redeemed {0}";
    public string SeatTokenAvailableText { get; init; } = "available";

    public string QrCodeAltText { get; init; } = string.Empty;
    public string PayButtonText { get; init; } = string.Empty;
    public string DownloadInvoiceText { get; init; } = string.Empty;
}
