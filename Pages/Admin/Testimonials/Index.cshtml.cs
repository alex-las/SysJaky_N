using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Authorization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Testimonials;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<IndexModel> _localizer;

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public IList<Testimonial> Testimonials { get; set; } = new List<Testimonial>();

    public async Task OnGetAsync()
    {
        Testimonials = await _context.Testimonials
            .AsNoTracking()
            .OrderByDescending(t => t.IsPublished)
            .ThenByDescending(t => t.ConsentGranted)
            .ThenBy(t => t.FullName)
            .ToListAsync();
    }
}
