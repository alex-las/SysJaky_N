using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System.IO;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminDashboardAccess)]
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private static readonly TimeZoneInfo PragueTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
    private static readonly string[] KnownCityNames =
    {
        "Praha",
        "Brno",
        "Ostrava",
        "Plzeň",
        "Liberec",
        "Olomouc",
        "Hradec Králové",
        "Pardubice",
        "České Budějovice",
        "Zlín"
    };

    private readonly ApplicationDbContext _context;
    public AnalyticsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("filters")]
    public async Task<ActionResult<FilterResponse>> GetFilters(CancellationToken cancellationToken)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PragueTimeZone);
        var defaultTo = DateOnly.FromDateTime(nowLocal.Date);
        var defaultFrom = defaultTo.AddDays(-29);

        var tags = await _context.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new FilterOption(t.Id, t.Name))
            .ToListAsync(cancellationToken);

        var normy = tags
            .Where(t => t.Name.Contains("ISO", StringComparison.OrdinalIgnoreCase)
                || t.Name.Contains("ČSN", StringComparison.OrdinalIgnoreCase)
                || t.Name.Contains("EN", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (normy.Count == 0)
        {
            normy = tags;
        }

        var knownCitySet = new HashSet<string>(KnownCityNames, StringComparer.OrdinalIgnoreCase);
        var mesta = tags
            .Where(t => knownCitySet.Contains(t.Name))
            .ToList();

        return Ok(new FilterResponse(defaultFrom, defaultTo, normy, mesta));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardResponse>> GetDashboard([FromQuery] AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var dashboard = await BuildDashboardAsync(query, cancellationToken);
        return Ok(dashboard);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var dashboard = await BuildDashboardAsync(query, cancellationToken);

        using var workbook = new XLWorkbook();
        var summarySheet = workbook.Worksheets.Add("Přehled");

        summarySheet.Cell(1, 1).Value = "Období";
        summarySheet.Cell(1, 2).Value = $"{dashboard.ObdobiOd:yyyy-MM-dd} – {dashboard.ObdobiDo:yyyy-MM-dd}";

        summarySheet.Cell(3, 1).Value = "Souhrn";
        summarySheet.Cell(4, 1).Value = "Tržby celkem";
        summarySheet.Cell(4, 2).Value = dashboard.Souhrn.CelkoveTrzby;
        summarySheet.Cell(5, 1).Value = "Změna tržeb";
        summarySheet.Cell(5, 2).Value = dashboard.Souhrn.ZmenaTrzebProcenta / 100d;
        summarySheet.Cell(5, 2).Style.NumberFormat.Format = "0.00%";
        summarySheet.Cell(6, 1).Value = "Objednávky";
        summarySheet.Cell(6, 2).Value = dashboard.Souhrn.Objednavky;
        summarySheet.Cell(7, 1).Value = "Průměrná hodnota objednávky";
        summarySheet.Cell(7, 2).Value = dashboard.Souhrn.PrumernaObjednavka;
        summarySheet.Cell(8, 1).Value = "Prodáno míst";
        summarySheet.Cell(8, 2).Value = dashboard.Souhrn.ProdanaMista;
        summarySheet.Cell(9, 1).Value = "Průměrná obsazenost";
        summarySheet.Cell(9, 2).Value = dashboard.Souhrn.PrumernaObsazenost / 100d;
        summarySheet.Cell(9, 2).Style.NumberFormat.Format = "0.00%";
        summarySheet.Cell(10, 1).Value = "Aktivní zákazníci";
        summarySheet.Cell(10, 2).Value = dashboard.Souhrn.AktivniZakaznici;
        summarySheet.Cell(11, 1).Value = "Noví zákazníci";
        summarySheet.Cell(11, 2).Value = dashboard.Souhrn.NoviZakaznici;

        summarySheet.Cell(4, 2).Style.NumberFormat.Format = "#,##0.00";
        summarySheet.Cell(7, 2).Style.NumberFormat.Format = "#,##0.00";

        summarySheet.Cell(13, 1).Value = "Top kurzy podle tržeb";
        summarySheet.Cell(14, 1).Value = "Kurz";
        summarySheet.Cell(14, 2).Value = "Tržby";
        summarySheet.Cell(14, 3).Value = "Prodáno míst";

        var topRow = 15;
        foreach (var kurz in dashboard.TopKurzy)
        {
            summarySheet.Cell(topRow, 1).Value = kurz.Nazev;
            summarySheet.Cell(topRow, 2).Value = kurz.Trzba;
            summarySheet.Cell(topRow, 3).Value = kurz.Mnozstvi;
            summarySheet.Cell(topRow, 2).Style.NumberFormat.Format = "#,##0.00";
            topRow++;
        }

        summarySheet.Cell(topRow + 1, 1).Value = "Vývoj tržeb";
        summarySheet.Cell(topRow + 2, 1).Value = "Datum";
        summarySheet.Cell(topRow + 2, 2).Value = "Tržby";
        summarySheet.Cell(topRow + 2, 3).Value = "Objednávky";
        summarySheet.Cell(topRow + 2, 4).Value = "Průměrná objednávka";

        var trendRow = topRow + 3;
        foreach (var bod in dashboard.Trend)
        {
            summarySheet.Cell(trendRow, 1).Value = bod.Datum.ToDateTime(TimeOnly.MinValue);
            summarySheet.Cell(trendRow, 1).Style.DateFormat.Format = "yyyy-mm-dd";
            summarySheet.Cell(trendRow, 2).Value = bod.Trzba;
            summarySheet.Cell(trendRow, 2).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(trendRow, 3).Value = bod.Objednavky;
            summarySheet.Cell(trendRow, 4).Value = bod.PrumernaObjednavka;
            summarySheet.Cell(trendRow, 4).Style.NumberFormat.Format = "#,##0.00";
            trendRow++;
        }

        summarySheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var fileName = $"report-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private async Task<DashboardResponse> BuildDashboardAsync(AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var filter = await BuildFilterContextAsync(query, cancellationToken);
        if (filter.HasCourseFilter && filter.CourseIds.Count == 0)
        {
            return DashboardResponse.Empty(filter.From, filter.To);
        }

        var fromUtc = filter.FromUtc;
        var toUtc = filter.ToUtc;

        var orderItemsQuery = _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order != null && oi.Order.Status == OrderStatus.Paid)
            .Where(oi => oi.Order!.CreatedAt >= fromUtc && oi.Order.CreatedAt <= toUtc)
            .Where(oi => oi.Course != null);

        if (filter.CourseIds.Count > 0)
        {
            orderItemsQuery = orderItemsQuery.Where(oi => filter.CourseIds.Contains(oi.CourseId));
        }

        var orderItemData = await orderItemsQuery
            .Select(oi => new OrderItemSnapshot(
                oi.OrderId,
                oi.Order!.CreatedAt,
                oi.Order.UserId,
                oi.Total,
                oi.Quantity,
                oi.CourseId,
                oi.Course!.Title))
            .ToListAsync(cancellationToken);

        var trend = BuildTrend(orderItemData);
        var topCourses = BuildTopCourses(orderItemData);

        var termSnapshots = await LoadTermSnapshotsAsync(filter, cancellationToken);
        var heatmap = BuildHeatmap(termSnapshots);

        var summary = await BuildSummaryAsync(orderItemData, termSnapshots, filter, cancellationToken);

        var conversions = await BuildConversionAsync(orderItemData, filter, cancellationToken);

        return new DashboardResponse(filter.From, filter.To, summary, trend, topCourses, conversions, heatmap);
    }

    private async Task<SummaryDto> BuildSummaryAsync(
        IReadOnlyCollection<OrderItemSnapshot> orderItems,
        IReadOnlyCollection<TermSnapshot> terms,
        FilterContext filter,
        CancellationToken cancellationToken)
    {
        var totalRevenue = orderItems.Sum(item => item.Total);
        var orderIds = orderItems.Select(item => item.OrderId).Distinct().ToList();
        var totalOrders = orderIds.Count;
        var averageOrder = totalOrders > 0 ? totalRevenue / totalOrders : 0m;
        var seatsSold = orderItems.Sum(item => item.Quantity);
        var occupancyAverage = terms.Count > 0
            ? terms.Average(term => term.Occupancy)
            : 0d;

        var customerIds = orderItems
            .Where(item => !string.IsNullOrWhiteSpace(item.UserId))
            .Select(item => item.UserId!)
            .Distinct()
            .ToList();

        var previousCustomers = customerIds.Count == 0
            ? new HashSet<string>()
            : (await _context.Orders
                .AsNoTracking()
                .Where(o => o.Status == OrderStatus.Paid)
                .Where(o => o.UserId != null)
                .Where(o => customerIds.Contains(o.UserId!))
                .Where(o => o.CreatedAt < filter.FromUtc)
                .Select(o => o.UserId!)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var newCustomers = customerIds.Count(id => !previousCustomers.Contains(id));

        var previousRevenue = await CalculateRevenueAsync(filter.PreviousFrom, filter.PreviousTo, filter.CourseIds, cancellationToken);
        var revenueChangePercent = previousRevenue > 0m
            ? (double)((totalRevenue - previousRevenue) / previousRevenue) * 100d
            : totalRevenue > 0m ? 100d : 0d;

        var periodLength = Math.Max(1, filter.To.DayNumber - filter.From.DayNumber + 1);

        return new SummaryDto(
            Math.Round(totalRevenue, 2),
            Math.Round(previousRevenue, 2),
            revenueChangePercent,
            totalOrders,
            Math.Round(averageOrder, 2),
            seatsSold,
            Math.Round(occupancyAverage * 100d, 2),
            customerIds.Count,
            newCustomers,
            periodLength);
    }

    private async Task<ConversionDto> BuildConversionAsync(
        IReadOnlyCollection<OrderItemSnapshot> orderItems,
        FilterContext filter,
        CancellationToken cancellationToken)
    {
        var payments = orderItems
            .Select(item => item.OrderId)
            .Distinct()
            .Count();

        var waitlistQuery = _context.WaitlistEntries
            .AsNoTracking()
            .Where(entry => entry.CreatedAtUtc >= filter.FromUtc && entry.CreatedAtUtc <= filter.ToUtc);

        if (filter.CourseIds.Count > 0)
        {
            waitlistQuery = waitlistQuery.Where(entry => filter.CourseIds.Contains(entry.CourseTerm!.CourseId));
        }

        var registrations = await waitlistQuery.CountAsync(cancellationToken);

        var visitsQuery = _context.LogEntries
            .AsNoTracking()
            .Where(log => log.Timestamp >= filter.FromUtc && log.Timestamp <= filter.ToUtc);

        var visits = await visitsQuery.CountAsync(cancellationToken);
        if (filter.CourseIds.Count > 0)
        {
            visits = Math.Max(registrations, payments);
        }
        else
        {
            visits = Math.Max(visits, Math.Max(registrations, payments));
        }

        var visitToRegistration = visits > 0 ? (double)registrations / visits * 100d : 0d;
        var registrationToPayment = registrations > 0 ? (double)payments / registrations * 100d : 0d;
        var visitToPayment = visits > 0 ? (double)payments / visits * 100d : 0d;

        return new ConversionDto(
            visits,
            registrations,
            payments,
            visitToRegistration,
            registrationToPayment,
            visitToPayment);
    }

    private static IReadOnlyList<SalesPointDto> BuildTrend(IEnumerable<OrderItemSnapshot> orderItems)
    {
        return orderItems
            .GroupBy(item => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(item.CreatedAtUtc, DateTimeKind.Utc),
                PragueTimeZone)))
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var revenue = group.Sum(item => item.Total);
                var orders = group.Select(item => item.OrderId).Distinct().Count();
                var average = orders > 0 ? revenue / orders : 0m;
                return new SalesPointDto(group.Key, Math.Round(revenue, 2), orders, Math.Round(average, 2));
            })
            .ToList();
    }

    private static IReadOnlyList<TopCourseDto> BuildTopCourses(IEnumerable<OrderItemSnapshot> orderItems)
    {
        return orderItems
            .GroupBy(item => new { item.CourseId, item.CourseTitle })
            .Select(group => new TopCourseDto(
                group.Key.CourseId,
                group.Key.CourseTitle,
                Math.Round(group.Sum(item => item.Total), 2),
                group.Sum(item => item.Quantity)))
            .OrderByDescending(course => course.Trzba)
            .ThenBy(course => course.Nazev)
            .Take(5)
            .ToList();
    }

    private async Task<IReadOnlyList<TermSnapshot>> LoadTermSnapshotsAsync(FilterContext filter, CancellationToken cancellationToken)
    {
        var termQuery = _context.CourseTerms
            .AsNoTracking()
            .Where(term => term.StartUtc >= filter.FromUtc && term.StartUtc <= filter.ToUtc)
            .Where(term => term.IsActive);

        if (filter.CourseIds.Count > 0)
        {
            termQuery = termQuery.Where(term => filter.CourseIds.Contains(term.CourseId));
        }

        var terms = await termQuery
            .Select(term => new TermSnapshot(term.StartUtc, term.Capacity, term.SeatsTaken))
            .ToListAsync(cancellationToken);

        return terms;
    }

    private static HeatmapDto BuildHeatmap(IEnumerable<TermSnapshot> terms)
    {
        var cells = terms
            .Select(term =>
            {
                var startUtc = DateTime.SpecifyKind(term.StartUtc, DateTimeKind.Utc);
                var local = TimeZoneInfo.ConvertTimeFromUtc(startUtc, PragueTimeZone);
                var occupancy = term.Capacity > 0
                    ? Math.Clamp((double)Math.Min(term.SeatsTaken, term.Capacity) / term.Capacity, 0d, 1d)
                    : 0d;
                return new
                {
                    Day = (int)local.DayOfWeek,
                    Hour = local.Hour,
                    occupancy,
                    term.Capacity,
                    Seats = Math.Min(term.SeatsTaken, term.Capacity)
                };
            })
            .GroupBy(item => new { item.Day, item.Hour })
            .Select(group => new HeatmapCellDto(
                group.Key.Day,
                group.Key.Hour,
                Math.Round(group.Average(item => item.occupancy), 4),
                group.Count(),
                group.Sum(item => item.Capacity),
                group.Sum(item => item.Seats)))
            .OrderBy(cell => cell.Den)
            .ThenBy(cell => cell.Hodina)
            .ToList();

        var maxOccupancy = cells.Count > 0 ? cells.Max(cell => cell.Obsazenost) : 0d;
        return new HeatmapDto(cells, maxOccupancy);
    }

    private async Task<decimal> CalculateRevenueAsync(DateOnly from, DateOnly to, IReadOnlyCollection<int> courseIds, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return 0m;
        }

        var fromUtc = ToUtc(from, TimeOnly.MinValue);
        var toUtc = ToUtc(to, TimeOnly.MaxValue);

        var query = _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order != null && oi.Order.Status == OrderStatus.Paid)
            .Where(oi => oi.Order!.CreatedAt >= fromUtc && oi.Order.CreatedAt <= toUtc);

        if (courseIds.Count > 0)
        {
            query = query.Where(oi => courseIds.Contains(oi.CourseId));
        }

        var result = await query.SumAsync(oi => (decimal?)oi.Total, cancellationToken);
        return Math.Round(result ?? 0m, 2);
    }

    private async Task<FilterContext> BuildFilterContextAsync(AnalyticsQuery query, CancellationToken cancellationToken)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PragueTimeZone);
        var defaultTo = DateOnly.FromDateTime(nowLocal.Date);
        var defaultFrom = defaultTo.AddDays(-29);

        var from = ParseDate(query.From, defaultFrom);
        var to = ParseDate(query.To, defaultTo);
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var periodLength = Math.Max(1, to.DayNumber - from.DayNumber + 1);
        var previousTo = from.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(periodLength - 1));

        var hasCourseFilter = (query.Normy?.Count ?? 0) > 0 || (query.Mesta?.Count ?? 0) > 0;
        var courseIds = new List<int>();

        if (hasCourseFilter)
        {
            var courseQuery = _context.Courses
                .AsNoTracking()
                .Where(course => course.IsActive);

            if (query.Normy is { Count: > 0 })
            {
                var norms = query.Normy;
                courseQuery = courseQuery.Where(course => course.CourseTags.Any(tag => norms.Contains(tag.TagId)));
            }

            if (query.Mesta is { Count: > 0 })
            {
                var cities = query.Mesta;
                courseQuery = courseQuery.Where(course => course.CourseTags.Any(tag => cities.Contains(tag.TagId)));
            }

            courseIds = await courseQuery
                .Select(course => course.Id)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        return new FilterContext(from, to, previousFrom, previousTo, courseIds, hasCourseFilter);
    }

    private static DateOnly ParseDate(string? input, DateOnly fallback)
    {
        if (!string.IsNullOrWhiteSpace(input) && DateOnly.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, PragueTimeZone);
    }

    private sealed record FilterContext(
        DateOnly From,
        DateOnly To,
        DateOnly PreviousFrom,
        DateOnly PreviousTo,
        IReadOnlyList<int> CourseIds,
        bool HasCourseFilter)
    {
        public DateTime FromUtc => ToUtc(From, TimeOnly.MinValue);
        public DateTime ToUtc => ToUtc(To, TimeOnly.MaxValue);
        public DateTime PreviousFromUtc => ToUtc(PreviousFrom, TimeOnly.MinValue);
        public DateTime PreviousToUtc => ToUtc(PreviousTo, TimeOnly.MaxValue);
    }

    public sealed record AnalyticsQuery
    {
        [FromQuery(Name = "from")]
        public string? From { get; init; }

        [FromQuery(Name = "to")]
        public string? To { get; init; }

        [FromQuery(Name = "normy")]
        public List<int> Normy { get; init; } = new();

        [FromQuery(Name = "mesta")]
        public List<int> Mesta { get; init; } = new();
    }

    public sealed record FilterResponse(
        DateOnly VychoziOd,
        DateOnly VychoziDo,
        IReadOnlyList<FilterOption> Normy,
        IReadOnlyList<FilterOption> Mesta);

    public sealed record FilterOption(int Id, string Name);

    public sealed record DashboardResponse(
        DateOnly ObdobiOd,
        DateOnly ObdobiDo,
        SummaryDto Souhrn,
        IReadOnlyList<SalesPointDto> Trend,
        IReadOnlyList<TopCourseDto> TopKurzy,
        ConversionDto Konverze,
        HeatmapDto Heatmap)
    {
        public static DashboardResponse Empty(DateOnly from, DateOnly to) => new(
            from,
            to,
            new SummaryDto(0m, 0m, 0d, 0, 0m, 0, 0d, 0, 0, Math.Max(1, to.DayNumber - from.DayNumber + 1)),
            Array.Empty<SalesPointDto>(),
            Array.Empty<TopCourseDto>(),
            new ConversionDto(0, 0, 0, 0d, 0d, 0d),
            new HeatmapDto(Array.Empty<HeatmapCellDto>(), 0d));
    }

    public sealed record SummaryDto(
        decimal CelkoveTrzby,
        decimal PredchoziTrzby,
        double ZmenaTrzebProcenta,
        int Objednavky,
        decimal PrumernaObjednavka,
        int ProdanaMista,
        double PrumernaObsazenost,
        int AktivniZakaznici,
        int NoviZakaznici,
        int DelkaObdobiDni);

    public sealed record SalesPointDto(DateOnly Datum, decimal Trzba, int Objednavky, decimal PrumernaObjednavka);

    public sealed record TopCourseDto(int KurzId, string Nazev, decimal Trzba, int Mnozstvi);

    public sealed record ConversionDto(
        int Navstevy,
        int Registrace,
        int Platby,
        double NavstevyNaRegistraci,
        double RegistraceNaPlatbu,
        double NavstevyNaPlatbu);

    public sealed record HeatmapDto(IReadOnlyList<HeatmapCellDto> Bunky, double MaxObsazenost);

    public sealed record HeatmapCellDto(
        int Den,
        int Hodina,
        double Obsazenost,
        int PocetTerminu,
        int KapacitaCelkem,
        int ObsazenaMista);

    private sealed record OrderItemSnapshot(
        int OrderId,
        DateTime CreatedAtUtc,
        string? UserId,
        decimal Total,
        int Quantity,
        int CourseId,
        string CourseTitle);

    private sealed record TermSnapshot(DateTime StartUtc, int Capacity, int SeatsTaken)
    {
        public double Occupancy => Capacity > 0
            ? Math.Clamp((double)Math.Min(SeatsTaken, Capacity) / Capacity, 0d, 1d)
            : 0d;
    }
}
