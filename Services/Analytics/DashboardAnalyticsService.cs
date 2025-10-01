using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Services.Analytics;

public class DashboardAnalyticsService
{
    internal static readonly string[] DnyVTydnu =
    {
        "Pondělí",
        "Úterý",
        "Středa",
        "Čtvrtek",
        "Pátek",
        "Sobota",
        "Neděle"
    };

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public DashboardAnalyticsService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<DashboardOverviewDto> GetOverviewAsync(AnalyticsFilter filter, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var (odUtc, doUtcExclusive) = filter.ToUtcRange();
        var kurzovyFiltr = await ResolveCourseFilterAsync(context, filter, cancellationToken);

        if (kurzovyFiltr?.Count == 0)
        {
            return DashboardOverviewDto.Empty(filter);
        }

        var zaplaceneObjednavky = context.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid && o.CreatedAt >= odUtc && o.CreatedAt < doUtcExclusive);

        if (kurzovyFiltr is { Count: > 0 })
        {
            zaplaceneObjednavky = zaplaceneObjednavky
                .Where(o => o.Items.Any(i => kurzovyFiltr.Contains(i.CourseId)));
        }

        var denniStatistiky = await zaplaceneObjednavky
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new
            {
                Datum = g.Key,
                Trzby = g.Sum(o => o.Total),
                Objednavky = g.Count(),
                Prumer = g.Average(o => o.Total)
            })
            .ToListAsync(cancellationToken);

        var statyPodleData = denniStatistiky.ToDictionary(
            x => DateOnly.FromDateTime(x.Datum),
            x => new Dennistat(x.Trzby, x.Objednavky, x.Prumer));

        var popisky = new List<string>();
        var trzby = new List<decimal>();
        var objednavky = new List<int>();
        var prumerneObjednavky = new List<decimal>();

        for (var datum = filter.Od; datum <= filter.Do; datum = datum.AddDays(1))
        {
            popisky.Add(datum.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (statyPodleData.TryGetValue(datum, out var stat))
            {
                trzby.Add(Math.Round(stat.Trzby, 2));
                objednavky.Add(stat.Objednavky);
                prumerneObjednavky.Add(Math.Round(stat.PrumernaObjednavka, 2));
            }
            else
            {
                trzby.Add(0m);
                objednavky.Add(0);
                prumerneObjednavky.Add(0m);
            }
        }

        var topKurzy = await context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order != null && oi.Order.Status == OrderStatus.Paid)
            .Where(oi => oi.Order!.CreatedAt >= odUtc && oi.Order.CreatedAt < doUtcExclusive)
            .Where(oi => kurzovyFiltr == null || kurzovyFiltr.Contains(oi.CourseId))
            .GroupBy(oi => new { oi.CourseId, Nazev = oi.Course != null ? oi.Course.Title : null })
            .Select(g => new TopCourseDto(
                g.Key.CourseId,
                g.Key.Nazev ?? $"Kurz #{g.Key.CourseId}",
                Math.Round(g.Sum(i => i.Total), 2),
                g.Sum(i => i.Quantity)))
            .OrderByDescending(k => k.Trzba)
            .ThenBy(k => k.Nazev)
            .Take(10)
            .ToListAsync(cancellationToken);

        var navstevy = await VypocitatNavstevyAsync(context, odUtc, doUtcExclusive, kurzovyFiltr, cancellationToken);

        var registrace = context.WaitlistEntries
            .AsNoTracking()
            .Where(w => w.CreatedAtUtc >= odUtc && w.CreatedAtUtc < doUtcExclusive);

        if (kurzovyFiltr is { Count: > 0 })
        {
            registrace = registrace.Where(w => w.CourseTerm != null && kurzovyFiltr.Contains(w.CourseTerm.CourseId));
        }

        var pocetRegistraci = await registrace.CountAsync(cancellationToken);
        var pocetPlateb = await zaplaceneObjednavky.CountAsync(cancellationToken);

        var konverze = new ConversionFunnelDto(
            navstevy,
            pocetRegistraci,
            pocetPlateb,
            navstevy == 0 ? 0 : Math.Round((double)pocetRegistraci / navstevy * 100d, 2),
            pocetRegistraci == 0 ? 0 : Math.Round((double)pocetPlateb / pocetRegistraci * 100d, 2),
            navstevy == 0 ? 0 : Math.Round((double)pocetPlateb / navstevy * 100d, 2));

        var heatmapa = await VytvoritHeatmapuAsync(context, filter, kurzovyFiltr, odUtc, doUtcExclusive, cancellationToken);

        var souhrn = await VytvoritSouhrnAsync(zaplaceneObjednavky, trzby, objednavky, cancellationToken);

        return new DashboardOverviewDto(popisky, trzby, objednavky, prumerneObjednavky, topKurzy, konverze, heatmapa, souhrn);
    }

    public async Task<RealtimeStatsDto> GetRealtimeStatsAsync(AnalyticsFilter filter, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var kurzovyFiltr = await ResolveCourseFilterAsync(context, filter, cancellationToken);

        if (kurzovyFiltr?.Count == 0)
        {
            return new RealtimeStatsDto(0, 0, 0m);
        }

        var hraniceOnline = DateTime.UtcNow.AddMinutes(-10);
        var dotazProgres = context.LessonProgresses
            .AsNoTracking()
            .Where(lp => lp.LastSeenUtc >= hraniceOnline);

        if (kurzovyFiltr is { Count: > 0 })
        {
            dotazProgres = dotazProgres.Where(lp => kurzovyFiltr.Contains(lp.Lesson.CourseId));
        }

        var onlineUzivatele = await dotazProgres
            .Select(lp => lp.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var hraniceKosiku = DateTime.UtcNow.AddHours(-2);
        var cekajiciObjednavky = context.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt >= hraniceKosiku);

        if (kurzovyFiltr is { Count: > 0 })
        {
            cekajiciObjednavky = cekajiciObjednavky
                .Where(o => o.Items.Any(i => kurzovyFiltr.Contains(i.CourseId)));
        }

        var pocetKosiku = await cekajiciObjednavky.CountAsync(cancellationToken);
        var hodnotaKosiku = await cekajiciObjednavky.SumAsync(o => (decimal?)o.Total, cancellationToken) ?? 0m;

        return new RealtimeStatsDto(onlineUzivatele, pocetKosiku, Math.Round(hodnotaKosiku, 2));
    }

    private static async Task<List<int>?> ResolveCourseFilterAsync(ApplicationDbContext context, AnalyticsFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Normy.Count == 0 && filter.Mesta.Count == 0)
        {
            return null;
        }

        var kurzy = context.Courses.AsNoTracking().AsQueryable();

        if (filter.Normy.Count > 0)
        {
            kurzy = kurzy.Where(c => c.CourseTags.Any(ct => filter.Normy.Contains(ct.TagId)));
        }

        if (filter.Mesta.Count > 0)
        {
            kurzy = kurzy.Where(c => c.CourseTags.Any(ct => filter.Mesta.Contains(ct.TagId)));
        }

        var vysledek = await kurzy.Select(c => c.Id).Distinct().ToListAsync(cancellationToken);
        return vysledek;
    }

    private static async Task<int> VypocitatNavstevyAsync(
        ApplicationDbContext context,
        DateTime odUtc,
        DateTime doUtcExclusive,
        IReadOnlyCollection<int>? kurzovyFiltr,
        CancellationToken cancellationToken)
    {
        var logy = await context.LogEntries
            .AsNoTracking()
            .Where(l => l.Timestamp >= odUtc && l.Timestamp < doUtcExclusive)
            .Where(l => l.Properties != null || l.Message != null)
            .Select(l => new { l.Properties, l.Message })
            .ToListAsync(cancellationToken);

        if (logy.Count == 0)
        {
            return 0;
        }

        if (kurzovyFiltr is { Count: > 0 })
        {
            return logy.Count(zaznam => ObsahujeKurzovouStopu(zaznam.Properties, zaznam.Message, kurzovyFiltr));
        }

        return logy.Count(zaznam => ObsahujeKurzovouStopu(zaznam.Properties, zaznam.Message, null));
    }

    private static async Task<HeatmapDto> VytvoritHeatmapuAsync(
        ApplicationDbContext context,
        AnalyticsFilter filter,
        IReadOnlyCollection<int>? kurzovyFiltr,
        DateTime odUtc,
        DateTime doUtcExclusive,
        CancellationToken cancellationToken)
    {
        var terminy = context.CourseTerms
            .AsNoTracking()
            .Where(t => t.StartUtc >= odUtc && t.StartUtc < doUtcExclusive);

        if (kurzovyFiltr is { Count: > 0 })
        {
            terminy = terminy.Where(t => kurzovyFiltr.Contains(t.CourseId));
        }

        var zaznamy = await terminy
            .Select(t => new { t.StartUtc, t.Capacity, t.SeatsTaken })
            .ToListAsync(cancellationToken);

        if (zaznamy.Count == 0)
        {
            return HeatmapDto.Empty;
        }

        var agregace = new Dictionary<(int Den, int Hodina), (decimal Soucet, int Pocet)>();
        var hodiny = new HashSet<int>();

        foreach (var z in zaznamy)
        {
            var den = PrevedDen(z.StartUtc.DayOfWeek);
            var hodina = z.StartUtc.Hour;
            var kapacita = Math.Max(z.Capacity, 0);
            var obsazeni = kapacita == 0 ? 0m : Math.Clamp((decimal)z.SeatsTaken / kapacita * 100m, 0m, 100m);

            hodiny.Add(hodina);

            var klic = (den, hodina);
            if (agregace.TryGetValue(klic, out var akumulace))
            {
                agregace[klic] = (akumulace.Soucet + obsazeni, akumulace.Pocet + 1);
            }
            else
            {
                agregace[klic] = (obsazeni, 1);
            }
        }

        var bunky = new List<HeatmapCellDto>();
        decimal maximum = 0m;

        foreach (var (klic, hodnota) in agregace)
        {
            var prumer = hodnota.Pocet == 0 ? 0m : Math.Round(hodnota.Soucet / hodnota.Pocet, 2);
            maximum = Math.Max(maximum, prumer);
            bunky.Add(new HeatmapCellDto(klic.Den, klic.Hodina, prumer));
        }

        bunky.Sort((a, b) =>
        {
            var denPorovnani = a.Den.CompareTo(b.Den);
            return denPorovnani != 0 ? denPorovnani : a.Hodina.CompareTo(b.Hodina);
        });

        var hodinySeznam = hodiny.OrderBy(h => h).ToArray();
        return new HeatmapDto(DnyVTydnu, hodinySeznam, bunky, maximum);
    }

    private static async Task<SummaryDto> VytvoritSouhrnAsync(
        IQueryable<Order> zaplaceneObjednavky,
        IReadOnlyCollection<decimal> trzby,
        IReadOnlyCollection<int> objednavky,
        CancellationToken cancellationToken)
    {
        var celkoveTrzby = Math.Round(trzby.Sum(), 2);
        var celkemObjednavek = objednavky.Sum();
        var prumernaObjednavka = celkemObjednavek == 0 ? 0m : Math.Round(celkoveTrzby / celkemObjednavek, 2);

        var unikatiZakaznici = await zaplaceneObjednavky
            .Where(o => o.UserId != null)
            .Select(o => o.UserId!)
            .Distinct()
            .CountAsync(cancellationToken);

        return new SummaryDto(celkoveTrzby, celkemObjednavek, prumernaObjednavka, unikatiZakaznici);
    }

    private static bool ObsahujeKurzovouStopu(string? vlastnosti, string? zprava, IReadOnlyCollection<int>? kurzovyFiltr)
    {
        if (string.IsNullOrWhiteSpace(vlastnosti) && string.IsNullOrWhiteSpace(zprava))
        {
            return false;
        }

        if (kurzovyFiltr is { Count: > 0 })
        {
            foreach (var id in kurzovyFiltr)
            {
                var hledany = $"/Courses/Details/{id}";
                if ((vlastnosti?.Contains(hledany, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (zprava?.Contains(hledany, StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    return true;
                }
            }

            return false;
        }

        return (vlastnosti?.Contains("/Courses", StringComparison.OrdinalIgnoreCase) ?? false)
            || (zprava?.Contains("/Courses", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static int PrevedDen(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => 0,
            DayOfWeek.Tuesday => 1,
            DayOfWeek.Wednesday => 2,
            DayOfWeek.Thursday => 3,
            DayOfWeek.Friday => 4,
            DayOfWeek.Saturday => 5,
            _ => 6
        };
    }

    private readonly record struct Dennistat(decimal Trzby, int Objednavky, decimal PrumernaObjednavka);
}

public sealed record AnalyticsFilter(DateOnly Od, DateOnly Do, IReadOnlyList<int> Normy, IReadOnlyList<int> Mesta)
{
    public (DateTime OdUtc, DateTime DoUtcExclusive) ToUtcRange()
    {
        var od = DateTime.SpecifyKind(Od.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var doValue = DateTime.SpecifyKind(Do.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        return (od, doValue);
    }
}

public sealed record DashboardOverviewDto(
    IReadOnlyList<string> Labels,
    IReadOnlyList<decimal> Revenue,
    IReadOnlyList<int> Orders,
    IReadOnlyList<decimal> AverageOrder,
    IReadOnlyList<TopCourseDto> TopCourses,
    ConversionFunnelDto Conversion,
    HeatmapDto Heatmap,
    SummaryDto Summary)
{
    public static DashboardOverviewDto Empty(AnalyticsFilter filter)
    {
        var popisky = new List<string>();
        var trzby = new List<decimal>();
        var objednavky = new List<int>();
        var prumery = new List<decimal>();

        for (var datum = filter.Od; datum <= filter.Do; datum = datum.AddDays(1))
        {
            popisky.Add(datum.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            trzby.Add(0m);
            objednavky.Add(0);
            prumery.Add(0m);
        }

        return new DashboardOverviewDto(
            popisky,
            trzby,
            objednavky,
            prumery,
            Array.Empty<TopCourseDto>(),
            new ConversionFunnelDto(0, 0, 0, 0, 0, 0),
            HeatmapDto.Empty,
            new SummaryDto(0m, 0, 0m, 0));
    }
}

public sealed record TopCourseDto(int CourseId, string Nazev, decimal Trzba, int Pocet);

public sealed record ConversionFunnelDto(
    int Navstevy,
    int Registrace,
    int Platby,
    double MieraNavstevaRegistrace,
    double MieraRegistracePlatba,
    double CelkovaMiera);

public sealed record HeatmapDto(
    IReadOnlyList<string> Dny,
    IReadOnlyList<int> Hodiny,
    IReadOnlyList<HeatmapCellDto> Bunky,
    decimal Maximum)
{
    public static HeatmapDto Empty => new(DashboardAnalyticsService.DnyVTydnu, Array.Empty<int>(), Array.Empty<HeatmapCellDto>(), 0m);
}

public sealed record HeatmapCellDto(int Den, int Hodina, decimal Hodnota);

public sealed record SummaryDto(decimal CelkoveTrzby, int PocetObjednavek, decimal PrumernaObjednavka, int UnikatniZakaznici);

public sealed record RealtimeStatsDto(int OnlineUzivatele, int AktivniKosiky, decimal HodnotaKosiku);
