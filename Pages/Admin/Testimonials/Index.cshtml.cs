using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Authorization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Testimonials;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
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
