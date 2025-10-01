using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using SysJaky_N.Authorization;
using SysJaky_N.Services.Analytics;

namespace SysJaky_N.Controllers;

[ApiController]
[Route("api/admin/analytics")]
[Authorize(Policy = AuthorizationPolicies.AdminDashboardAccess)]
public class AnalyticsController : ControllerBase
{
    private readonly DashboardAnalyticsService _analytics;

    public AnalyticsController(DashboardAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewDto>> ZiskatPrehledAsync([FromQuery] FiltrAnalytiky dotaz, CancellationToken cancellationToken)
    {
        var filter = dotaz.ToFilter();
        var data = await _analytics.GetOverviewAsync(filter, cancellationToken);
        return Ok(data);
    }

    [HttpGet("realtime")]
    public async Task<ActionResult<RealtimeStatsDto>> ZiskatRealtimeAsync([FromQuery] FiltrAnalytiky dotaz, CancellationToken cancellationToken)
    {
        var filter = dotaz.ToFilter();
        var data = await _analytics.GetRealtimeStatsAsync(filter, cancellationToken);
        return Ok(data);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportovatAsync([FromQuery] FiltrAnalytiky dotaz, CancellationToken cancellationToken)
    {
        var filter = dotaz.ToFilter();
        var prehled = await _analytics.GetOverviewAsync(filter, cancellationToken);
        var realtime = await _analytics.GetRealtimeStatsAsync(filter, cancellationToken);

        using var paket = new ExcelPackage();

        VytvoritListSouhrn(paket, filter, prehled, realtime);
        VytvoritListProdeje(paket, prehled);
        VytvoritListTopKurzy(paket, prehled.TopCourses);
        VytvoritListKonverze(paket, prehled.Conversion);
        VytvoritListHeatmapa(paket, prehled.Heatmap);

        var soubor = paket.GetAsByteArray();
        var nazev = $"dashboard-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        return File(soubor, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nazev);
    }

    private static void VytvoritListSouhrn(ExcelPackage paket, AnalyticsFilter filter, DashboardOverviewDto prehled, RealtimeStatsDto realtime)
    {
        var list = paket.Workbook.Worksheets.Add("Souhrn");
        list.Cells[1, 1].Value = "Období";
        list.Cells[1, 2].Value = $"{filter.Od:dd.MM.yyyy} – {filter.Do:dd.MM.yyyy}";

        list.Cells[2, 1].Value = "Celkové tržby";
        list.Cells[2, 2].Value = prehled.Summary.CelkoveTrzby;

        list.Cells[3, 1].Value = "Zaplacené objednávky";
        list.Cells[3, 2].Value = prehled.Summary.PocetObjednavek;

        list.Cells[4, 1].Value = "Průměrná objednávka";
        list.Cells[4, 2].Value = prehled.Summary.PrumernaObjednavka;

        list.Cells[5, 1].Value = "Unikátní zákazníci";
        list.Cells[5, 2].Value = prehled.Summary.UnikatniZakaznici;

        list.Cells[7, 1].Value = "Online účastníci";
        list.Cells[7, 2].Value = realtime.OnlineUzivatele;

        list.Cells[8, 1].Value = "Rozpracované košíky";
        list.Cells[8, 2].Value = realtime.AktivniKosiky;

        list.Cells[9, 1].Value = "Hodnota v košících";
        list.Cells[9, 2].Value = realtime.HodnotaKosiku;

        list.Cells[2, 2, 9, 2].Style.Numberformat.Format = "#,##0.00";
        list.Cells.AutoFitColumns();
    }

    private static void VytvoritListProdeje(ExcelPackage paket, DashboardOverviewDto prehled)
    {
        var list = paket.Workbook.Worksheets.Add("Prodeje");
        list.Cells[1, 1].Value = "Datum";
        list.Cells[1, 2].Value = "Tržby";
        list.Cells[1, 3].Value = "Objednávky";
        list.Cells[1, 4].Value = "Průměrná objednávka";

        for (var i = 0; i < prehled.Labels.Count; i++)
        {
            list.Cells[i + 2, 1].Value = prehled.Labels[i];
            list.Cells[i + 2, 2].Value = prehled.Revenue[i];
            list.Cells[i + 2, 3].Value = prehled.Orders[i];
            list.Cells[i + 2, 4].Value = prehled.AverageOrder[i];
        }

        list.Cells[2, 2, prehled.Labels.Count + 1, 4].Style.Numberformat.Format = "#,##0.00";
        list.Cells.AutoFitColumns();
    }

    private static void VytvoritListTopKurzy(ExcelPackage paket, IReadOnlyList<TopCourseDto> topKurzy)
    {
        var list = paket.Workbook.Worksheets.Add("Top kurzy");
        list.Cells[1, 1].Value = "Kurz";
        list.Cells[1, 2].Value = "Tržby";
        list.Cells[1, 3].Value = "Prodáno";

        for (var i = 0; i < topKurzy.Count; i++)
        {
            list.Cells[i + 2, 1].Value = topKurzy[i].Nazev;
            list.Cells[i + 2, 2].Value = topKurzy[i].Trzba;
            list.Cells[i + 2, 3].Value = topKurzy[i].Pocet;
        }

        list.Cells[2, 2, topKurzy.Count + 1, 2].Style.Numberformat.Format = "#,##0.00";
        list.Cells.AutoFitColumns();
    }

    private static void VytvoritListKonverze(ExcelPackage paket, ConversionFunnelDto konverze)
    {
        var list = paket.Workbook.Worksheets.Add("Konverze");
        list.Cells[1, 1].Value = "Návštěvy";
        list.Cells[1, 2].Value = konverze.Navstevy;

        list.Cells[2, 1].Value = "Registrace";
        list.Cells[2, 2].Value = konverze.Registrace;

        list.Cells[3, 1].Value = "Platby";
        list.Cells[3, 2].Value = konverze.Platby;

        list.Cells[5, 1].Value = "Míra návštěva → registrace";
        list.Cells[5, 2].Value = konverze.MieraNavstevaRegistrace / 100d;

        list.Cells[6, 1].Value = "Míra registrace → platba";
        list.Cells[6, 2].Value = konverze.MieraRegistracePlatba / 100d;

        list.Cells[7, 1].Value = "Celková míra";
        list.Cells[7, 2].Value = konverze.CelkovaMiera / 100d;

        list.Cells[5, 2, 7, 2].Style.Numberformat.Format = "0.00%";
        list.Cells.AutoFitColumns();
    }

    private static void VytvoritListHeatmapa(ExcelPackage paket, HeatmapDto heatmapa)
    {
        var list = paket.Workbook.Worksheets.Add("Heatmapa");
        list.Cells[1, 1].Value = "Den";
        list.Cells[1, 2].Value = "Hodina";
        list.Cells[1, 3].Value = "Průměrné zaplnění";

        for (var i = 0; i < heatmapa.Bunky.Count; i++)
        {
            var bunka = heatmapa.Bunky[i];
            var radek = i + 2;
            list.Cells[radek, 1].Value = heatmapa.Dny[Math.Clamp(bunka.Den, 0, heatmapa.Dny.Count - 1)];
            list.Cells[radek, 2].Value = $"{bunka.Hodina:00}:00";
            list.Cells[radek, 3].Value = bunka.Hodnota / 100m;
        }

        list.Cells[2, 3, heatmapa.Bunky.Count + 1, 3].Style.Numberformat.Format = "0.00%";
        list.Cells.AutoFitColumns();
    }

    public class FiltrAnalytiky
    {
        public DateTime? Od { get; set; }
        public DateTime? Do { get; set; }
        public List<int> Normy { get; set; } = new();
        public List<int> Mesta { get; set; } = new();

        public AnalyticsFilter ToFilter()
        {
            var dnes = DateOnly.FromDateTime(DateTime.UtcNow);
            var od = Od.HasValue ? DateOnly.FromDateTime(Od.Value.ToUniversalTime()) : dnes.AddDays(-29);
            var doDatum = Do.HasValue ? DateOnly.FromDateTime(Do.Value.ToUniversalTime()) : dnes;

            if (od > doDatum)
            {
                (od, doDatum) = (doDatum, od);
            }

            var normy = Normy?.Distinct().ToList() ?? new List<int>();
            var mesta = Mesta?.Distinct().ToList() ?? new List<int>();

            return new AnalyticsFilter(od, doDatum, normy, mesta);
        }
    }
}
