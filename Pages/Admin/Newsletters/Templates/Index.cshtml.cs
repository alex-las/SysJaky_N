using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Newsletters.Templates;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IList<NewsletterTemplate> Templates { get; private set; } = new List<NewsletterTemplate>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Templates = await _context.NewsletterTemplates
            .AsNoTracking()
            .Include(template => template.Regions)
            .OrderBy(template => template.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
