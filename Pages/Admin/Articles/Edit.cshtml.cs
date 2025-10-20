using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Articles;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<EditModel> _localizer;

    [BindProperty]
    public Article Article { get; set; } = new();

    public EditModel(ApplicationDbContext context, IStringLocalizer<EditModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var article = await _context.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (article == null)
        {
            return NotFound();
        }

        Article = article;

        if (Article.PublishedAtUtc.HasValue)
        {
            Article.PublishedAtUtc = DateTime.SpecifyKind(Article.PublishedAtUtc.Value, DateTimeKind.Utc).ToLocalTime();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        NormalizePublicationFields();

        if (!ModelState.IsValid)
        {
            RestoreLocalPublicationTime();
            return Page();
        }

        var article = await _context.Articles.FindAsync(Article.Id);
        if (article == null)
        {
            return NotFound();
        }

        article.Title = Article.Title;
        article.Content = Article.Content;
        article.IsPublished = Article.IsPublished;
        article.PublishedAtUtc = Article.PublishedAtUtc;
        article.UpdatedAtUtc = Article.UpdatedAtUtc;
        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private void NormalizePublicationFields()
    {
        var now = DateTime.UtcNow;

        if (Article.IsPublished)
        {
            if (Article.PublishedAtUtc.HasValue)
            {
                var publishedAt = Article.PublishedAtUtc.Value;
                Article.PublishedAtUtc = DateTime.SpecifyKind(publishedAt, DateTimeKind.Local).ToUniversalTime();
            }
            else
            {
                Article.PublishedAtUtc = now;
            }
        }
        else
        {
            Article.PublishedAtUtc = null;
        }

        Article.UpdatedAtUtc = now;
    }

    private void RestoreLocalPublicationTime()
    {
        if (Article.PublishedAtUtc.HasValue)
        {
            Article.PublishedAtUtc = DateTime.SpecifyKind(Article.PublishedAtUtc.Value, DateTimeKind.Utc).ToLocalTime();
        }
    }
}
