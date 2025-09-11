using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Articles;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IList<Article> Articles { get; set; } = new List<Article>();

    [BindProperty(SupportsGet = true)]
    public string? SearchString { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int TotalPages { get; set; }

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task OnGetAsync()
    {
        const int pageSize = 10;
        var query = _context.Articles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(SearchString))
        {
            var pattern = $"%{SearchString.Trim()}%";
            query = query.Where(a => EF.Functions.Like(a.Title, pattern));
        }

        query = query.OrderByDescending(a => a.CreatedAt);

        var count = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        Articles = await query.Skip((PageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
    }
}
