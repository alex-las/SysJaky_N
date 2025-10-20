using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Articles;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<IndexModel> _localizer;

    public IList<Article> Articles { get; set; } = new List<Article>();

    [BindProperty(SupportsGet = true)]
    public string? PublicationFilter { get; set; }

    public IEnumerable<SelectListItem> PublicationFilterOptions { get; private set; } = Enumerable.Empty<SelectListItem>();

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public async Task OnGetAsync()
    {
        var now = DateTime.UtcNow;
        var query = _context.Articles.AsNoTracking();

        switch (PublicationFilter)
        {
            case "published":
                query = query.Where(a => a.IsPublished && a.PublishedAtUtc <= now);
                break;
            case "scheduled":
                query = query.Where(a => a.IsPublished && a.PublishedAtUtc > now);
                break;
            case "draft":
                query = query.Where(a => !a.IsPublished);
                break;
        }

        Articles = await query
            .OrderByDescending(a => a.PublishedAtUtc ?? a.CreatedAt)
            .ThenByDescending(a => a.UpdatedAtUtc)
            .ToListAsync();

        PublicationFilterOptions = new List<SelectListItem>
        {
            new(GetString("FilterAll", "Vše"), string.Empty, string.IsNullOrEmpty(PublicationFilter)),
            new(GetString("FilterPublished", "Publikované"), "published", PublicationFilter == "published"),
            new(GetString("FilterScheduled", "Naplánované"), "scheduled", PublicationFilter == "scheduled"),
            new(GetString("FilterDraft", "Koncepty"), "draft", PublicationFilter == "draft"),
        };
    }

    private string GetString(string key, string fallback)
    {
        var localized = _localizer[key];
        return localized.ResourceNotFound ? fallback : localized.Value;
    }
}
