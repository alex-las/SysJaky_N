using System.Globalization;

namespace SysJaky_N.Models;

public record IsoBadgeViewModel(string Label, string Code);

public class CourseCardViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Level { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int Duration { get; init; }
    public string DurationDisplay { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public string DateDisplay { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string PriceDisplay { get; init; } = string.Empty;
    public string? CoverImageUrl { get; init; }
    public string? PopoverHtml { get; init; }
    public string DetailsUrl { get; init; } = string.Empty;
    public string AddToCartUrl { get; init; } = string.Empty;
    public string WishlistUrl { get; init; } = string.Empty;
    public IReadOnlyList<IsoBadgeViewModel> IsoBadges { get; init; } = Array.Empty<IsoBadgeViewModel>();
    public int? DaysUntilStart { get; init; }
    public int Capacity { get; init; }
    public int SeatsTaken { get; init; }
    public bool HasCertificate { get; init; }
    public string PreviewContent { get; init; } = string.Empty;

    public double OccupancyPercent => Capacity <= 0
        ? 0d
        : Math.Clamp((double)SeatsTaken / Capacity * 100d, 0d, 100d);

    public static string BuildDurationDisplay(int duration, CultureInfo culture)
        => string.Format(culture, "{0} min", duration);
}
