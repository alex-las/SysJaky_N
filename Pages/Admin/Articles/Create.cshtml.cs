using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Articles;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<CreateModel> _localizer;

    public CreateModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<CreateModel> localizer)
    {
        _context = context;
        _userManager = userManager;
        _localizer = localizer;
    }

    [BindProperty]
    public Article Article { get; set; } = new();

    public void OnGet()
    {
        Article.IsPublished = false;
        Article.PublishedAtUtc = null;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        NormalizePublicationFields();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Article.AuthorId = _userManager.GetUserId(User);
        Article.CreatedAt = DateTime.UtcNow;

        _context.Articles.Add(Article);
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
}
