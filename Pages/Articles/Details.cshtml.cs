using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Articles;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<DetailsModel> _localizer;

    public Article? Article { get; set; }

    public DetailsModel(ApplicationDbContext context, IStringLocalizer<DetailsModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var now = DateTime.UtcNow;
        Article = await _context.Articles
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.IsPublished && a.PublishedAtUtc <= now);

        if (Article == null)
        {
            return NotFound();
        }

        return Page();
    }
}
