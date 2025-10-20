using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Articles;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<DeleteModel> _localizer;

    [BindProperty]
    public Article? Article { get; set; }

    public DeleteModel(ApplicationDbContext context, IStringLocalizer<DeleteModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Article = await _context.Articles.FirstOrDefaultAsync(a => a.Id == id);
        if (Article == null)
        {
            return NotFound();
        }

        if (IsAjaxRequest())
        {
            return Partial("_DeleteModal", this);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var article = await _context.Articles.FindAsync(id);
        if (article != null)
        {
            _context.Articles.Remove(article);
            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = $"Článek \"{article.Title}\" byl smazán.";
        }

        if (IsAjaxRequest())
        {
            return new JsonResult(new { success = true });
        }

        return RedirectToPage("Index");
    }

    private bool IsAjaxRequest() => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
