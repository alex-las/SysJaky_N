using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Testimonials;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IList<Testimonial> Testimonials { get; private set; } = new List<Testimonial>();

    public async Task OnGetAsync()
    {
        Testimonials = await _context.Testimonials
            .AsNoTracking()
            .Where(t => t.IsPublished && t.ConsentGranted)
            .OrderByDescending(t => t.ConsentGrantedAtUtc ?? DateTime.MinValue)
            .ThenBy(t => t.FullName)
            .ToListAsync();
    }
}
