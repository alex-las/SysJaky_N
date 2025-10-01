using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Authorization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Testimonials;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DetailsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public Testimonial Testimonial { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var testimonial = await _context.Testimonials.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (testimonial == null)
        {
            return NotFound();
        }

        Testimonial = testimonial;
        return Page();
    }
}
