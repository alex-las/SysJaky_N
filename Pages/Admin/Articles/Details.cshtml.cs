using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Articles;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public Article? Article { get; set; }

    public DetailsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Article = await _context.Articles.FirstOrDefaultAsync(a => a.Id == id);
        if (Article == null)
        {
            return NotFound();
        }
        return Page();
    }
}
