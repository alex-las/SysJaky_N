using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Authorization;
using SysJaky_N.Data;

namespace SysJaky_N.Pages.Admin.Dashboard;

[Authorize(Policy = AuthorizationPolicies.AdminDashboardAccess)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    private static readonly string[] ZnamaMesta = new[]
    {
        "Praha",
        "Brno",
        "Ostrava",
        "Plzeň",
        "Liberec",
        "Olomouc",
        "Ústí nad Labem",
        "Hradec Králové",
        "České Budějovice",
        "Pardubice",
        "Zlín"
    };

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IReadOnlyList<VolbaFiltru> VolbyNorem { get; private set; } = Array.Empty<VolbaFiltru>();
    public IReadOnlyList<VolbaFiltru> VolbyMest { get; private set; } = Array.Empty<VolbaFiltru>();
    public DateOnly VychoziOd { get; private set; }
    public DateOnly VychoziDo { get; private set; }

    public async Task OnGetAsync()
    {
        VychoziDo = DateOnly.FromDateTime(DateTime.UtcNow);
        VychoziOd = VychoziDo.AddDays(-29);

        var vsechnyStitky = await _context.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new VolbaFiltru(t.Id, t.Name))
            .ToListAsync();

        var normy = vsechnyStitky
            .Where(t => t.Nazev.Contains("ISO", StringComparison.OrdinalIgnoreCase)
                        || t.Nazev.Contains("ČSN", StringComparison.OrdinalIgnoreCase)
                        || t.Nazev.Contains("EN", StringComparison.OrdinalIgnoreCase))
            .ToList();
        VolbyNorem = normy.Count > 0 ? normy : vsechnyStitky;

        var znamaMesta = new HashSet<string>(ZnamaMesta, StringComparer.OrdinalIgnoreCase);
        VolbyMest = vsechnyStitky
            .Where(t => znamaMesta.Contains(t.Nazev))
            .ToList();
    }

    public record VolbaFiltru(int Id, string Nazev);
}
