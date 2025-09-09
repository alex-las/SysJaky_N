using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Articles;

[Authorize(Roles = "Admin")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    [BindProperty]
    public Article Article { get; set; } = new();

    public EditModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Article = await _context.Articles.FindAsync(id);
        if (Article == null)
        {
            return NotFound();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var article = await _context.Articles.FindAsync(Article.Id);
        if (article == null)
        {
            return NotFound();
        }

        article.Title = Article.Title;
        article.Content = Article.Content;
        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
