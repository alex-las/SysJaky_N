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
            SearchString = SearchString.Trim();
            var pattern = $"%{SearchString}%";
            query = query.Where(a =>
                EF.Functions.Like(a.Title, pattern) ||
                EF.Functions.Like(a.Content, pattern));
        }

        query = query.OrderByDescending(a => a.CreatedAt);

        var count = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);

        if (TotalPages == 0)
        {
            PageNumber = 1;
        }
        else if (PageNumber < 1)
        {
            PageNumber = 1;
        }
        else if (PageNumber > TotalPages)
        {
            PageNumber = TotalPages;
        }

        var skip = (PageNumber - 1) * pageSize;
        Articles = await query.Skip(skip).Take(pageSize).ToListAsync();
    }
}
