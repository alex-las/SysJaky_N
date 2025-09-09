using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Articles;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IList<Article> Articles { get; set; } = new List<Article>();

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task OnGetAsync()
    {
        Articles = await _context.Articles
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }
}
