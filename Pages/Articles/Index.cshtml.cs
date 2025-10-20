using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Articles;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<IndexModel> _localizer;

    public IList<Article> Articles { get; set; } = new List<Article>();

    [BindProperty(SupportsGet = true)]
    public string? SearchString { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int TotalPages { get; set; }

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public async Task OnGetAsync()
    {
        const int pageSize = 10;
        var now = DateTime.UtcNow;
        var query = _context.Articles
            .Where(a => a.IsPublished && (a.PublishedAtUtc ?? a.CreatedAt) <= now)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(SearchString))
        {
            var pattern = $"%{SearchString.Trim()}%";
            query = query.Where(a => EF.Functions.Like(a.Title, pattern));
        }

        query = query.OrderByDescending(a => a.PublishedAtUtc ?? a.CreatedAt);

        var count = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        Articles = await query.Skip((PageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
    }
}
