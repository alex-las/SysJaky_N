using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Newsletters;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DetailsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public NewsletterIssue Issue { get; private set; } = default!;

    public IList<NewsletterIssueDelivery> Deliveries { get; private set; } = new List<NewsletterIssueDelivery>();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var issue = await _context.NewsletterIssues
            .AsNoTracking()
            .Include(i => i.NewsletterTemplate)
            .Include(i => i.Categories)
                .ThenInclude(category => category.NewsletterSectionCategory)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (issue is null)
        {
            return NotFound();
        }

        Issue = issue;
        ViewData["Title"] = $"Newsletter: {issue.Subject}";

        Deliveries = await _context.NewsletterIssueDeliveries
            .AsNoTracking()
            .Where(delivery => delivery.NewsletterIssueId == id)
            .Include(delivery => delivery.NewsletterSubscriber)
            .OrderByDescending(delivery => delivery.SentUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Page();
    }
}
