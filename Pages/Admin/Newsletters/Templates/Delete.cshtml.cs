using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Newsletters.Templates;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DeleteModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public NewsletterTemplate? Template { get; private set; }

    public int IssueCount { get; private set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        var template = await _context.NewsletterTemplates
            .Include(t => t.Regions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (template is null)
        {
            return NotFound();
        }

        var existingIssues = await _context.NewsletterIssues
            .AsNoTracking()
            .CountAsync(issue => issue.NewsletterTemplateId == id, cancellationToken)
            .ConfigureAwait(false);

        if (existingIssues > 0)
        {
            ErrorMessage = "Šablonu nelze odstranit, protože je použita u existujících newsletterů.";
            await LoadAsync(id, cancellationToken).ConfigureAwait(false);
            IssueCount = existingIssues;
            return Page();
        }

        _context.NewsletterTemplates.Remove(template);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return RedirectToPage("Index");
    }

    private async Task<bool> LoadAsync(int id, CancellationToken cancellationToken)
    {
        Template = await _context.NewsletterTemplates
            .AsNoTracking()
            .Include(t => t.Regions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (Template is null)
        {
            return false;
        }

        IssueCount = await _context.NewsletterIssues
            .AsNoTracking()
            .CountAsync(issue => issue.NewsletterTemplateId == id, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
