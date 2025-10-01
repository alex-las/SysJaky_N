using System;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SysJaky_N.Models;

namespace SysJaky_N.Models.ViewModels;

public sealed class CourseCardViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? CoverImageUrl { get; init; }
    public DateTime? StartDate { get; init; }
    public decimal Price { get; init; }
    public CourseLevel Level { get; init; }
    public CourseMode Mode { get; init; }
    public CourseType Type { get; init; }
    public int Duration { get; init; }
    public string? PopoverHtml { get; init; }
    public string? IsoCertification { get; init; }
    public string IsoIcon { get; init; } = "bi-patch-check";
    public int OccupancyPercent { get; init; }
    public int? Capacity { get; init; }
    public int? SeatsTaken { get; init; }
    public bool HasCertificate { get; init; }
    public string? CertificateLabel { get; init; }
    public string PreviewText { get; init; } = string.Empty;
    public bool IsWishlisted { get; init; }
    public string DetailsUrl { get; init; } = string.Empty;
    public string AddToCartUrl { get; init; } = string.Empty;
    public string DateDisplay { get; init; } = string.Empty;
    public string PriceDisplay { get; init; } = string.Empty;
    public string DurationDisplay { get; init; } = string.Empty;
}

public static class CourseCardViewModelFactory
{
    private static readonly string[] CertificateKeywords = new[] { "certifik", "certificate" };

    public static CourseCardViewModel Create(
        Course course,
        CourseTerm? upcomingTerm,
        ISet<int>? wishlistedCourseIds,
        IUrlHelper urlHelper,
        CultureInfo culture)
    {
        if (course == null)
        {
            throw new ArgumentNullException(nameof(course));
        }

        if (urlHelper == null)
        {
            throw new ArgumentNullException(nameof(urlHelper));
        }

        if (culture == null)
        {
            culture = CultureInfo.CurrentCulture;
        }

        var startDate = ResolveStartDate(course, upcomingTerm);
        var occupancyPercent = CalculateOccupancy(upcomingTerm);
        var (capacity, seatsTaken) = (upcomingTerm?.Capacity, upcomingTerm?.SeatsTaken);
        var isoCertification = ResolveIsoCertification(course);
        var isoIcon = ResolveIsoIcon(isoCertification);
        var certificateLabel = ResolveCertificateLabel(course);
        var hasCertificate = !string.IsNullOrWhiteSpace(certificateLabel);
        var preview = BuildPreview(course.Description);
        var dateDisplay = startDate.HasValue
            ? startDate.Value.ToString("d", culture)
            : course.Date.ToString("d", culture);
        var priceDisplay = course.Price.ToString("C", culture);
        var durationDisplay = string.Format(culture, "{0} min", course.Duration);
        var detailsUrl = urlHelper.Page("/Courses/Details", new { id = course.Id }) ?? $"/Courses/Details/{course.Id}";
        var addToCartUrl = urlHelper.Page("/Courses/Index", pageHandler: "AddToCart") ?? "/Courses/Index?handler=AddToCart";

        return new CourseCardViewModel
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            CoverImageUrl = course.CoverImageUrl,
            StartDate = startDate,
            Price = course.Price,
            Level = course.Level,
            Mode = course.Mode,
            Type = course.Type,
            Duration = course.Duration,
            PopoverHtml = course.PopoverHtml,
            IsoCertification = isoCertification,
            IsoIcon = isoIcon,
            OccupancyPercent = occupancyPercent,
            Capacity = capacity,
            SeatsTaken = seatsTaken,
            HasCertificate = hasCertificate,
            CertificateLabel = certificateLabel,
            PreviewText = preview,
            IsWishlisted = wishlistedCourseIds?.Contains(course.Id) ?? false,
            DetailsUrl = detailsUrl,
            AddToCartUrl = addToCartUrl,
            DateDisplay = dateDisplay,
            PriceDisplay = priceDisplay,
            DurationDisplay = durationDisplay
        };
    }

    private static DateTime? ResolveStartDate(Course course, CourseTerm? upcomingTerm)
    {
        if (upcomingTerm?.StartUtc != null)
        {
            try
            {
                return TimeZoneInfo.ConvertTimeFromUtc(upcomingTerm.StartUtc, TimeZoneInfo.Local);
            }
            catch (ArgumentException)
            {
                return upcomingTerm.StartUtc;
            }
        }

        if (course.Date != default)
        {
            return course.Date;
        }

        return null;
    }

    private static int CalculateOccupancy(CourseTerm? term)
    {
        if (term == null || term.Capacity <= 0)
        {
            return 0;
        }

        var percentage = (int)Math.Round((double)term.SeatsTaken * 100d / term.Capacity);
        return Math.Max(0, Math.Min(100, percentage));
    }

    private static string? ResolveIsoCertification(Course course)
    {
        var tag = course.CourseTags
            ?.Select(ct => ct.Tag?.Name)
            .FirstOrDefault(name => name != null && name.Contains("ISO", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(tag))
        {
            return tag;
        }

        if (!string.IsNullOrWhiteSpace(course.CourseGroup?.Name)
            && course.CourseGroup.Name.Contains("ISO", StringComparison.OrdinalIgnoreCase))
        {
            return course.CourseGroup.Name;
        }

        return null;
    }

    private static string ResolveIsoIcon(string? isoCertification)
    {
        if (string.IsNullOrWhiteSpace(isoCertification))
        {
            return "bi-patch-check";
        }

        if (isoCertification.Contains("14001", StringComparison.OrdinalIgnoreCase))
        {
            return "bi-globe2";
        }

        if (isoCertification.Contains("45001", StringComparison.OrdinalIgnoreCase))
        {
            return "bi-shield-shaded";
        }

        if (isoCertification.Contains("27001", StringComparison.OrdinalIgnoreCase))
        {
            return "bi-shield-lock";
        }

        return "bi-patch-check";
    }

    private static string? ResolveCertificateLabel(Course course)
    {
        if (course.CourseTags == null)
        {
            return null;
        }

        foreach (var tag in course.CourseTags)
        {
            var name = tag.Tag?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (CertificateKeywords.Any(keyword => name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }
        }

        return null;
    }

    private static string BuildPreview(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var normalized = description.Replace("\r", " ").Replace('\n', ' ');
        if (normalized.Length <= 180)
        {
            return normalized.Trim();
        }

        return normalized.AsSpan(0, 180).ToString().Trim() + "…";
    }
}
