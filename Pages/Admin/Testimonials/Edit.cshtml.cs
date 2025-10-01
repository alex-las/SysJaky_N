using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Authorization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Testimonials;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public EditModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Testimonial Testimonial { get; set; } = new();

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

    public async Task<IActionResult> OnPostAsync()
    {
        var existing = await _context.Testimonials.FirstOrDefaultAsync(t => t.Id == Testimonial.Id);
        if (existing == null)
        {
            return NotFound();
        }

        if (Testimonial.IsPublished && !Testimonial.ConsentGranted)
        {
            ModelState.AddModelError(nameof(Testimonial.ConsentGranted), "Bez souhlasu nelze referenci publikovat.");
        }

        if (!ModelState.IsValid)
        {
            Testimonial.ConsentGrantedAtUtc = existing.ConsentGrantedAtUtc;
            return Page();
        }

        existing.FullName = Testimonial.FullName;
        existing.Position = Testimonial.Position;
        existing.Company = Testimonial.Company;
        existing.PhotoUrl = Testimonial.PhotoUrl;
        existing.PhotoAltText = Testimonial.PhotoAltText;
        existing.Quote = Testimonial.Quote;
        existing.Rating = Testimonial.Rating;
        existing.ConsentGranted = Testimonial.ConsentGranted;

        if (Testimonial.ConsentGranted)
        {
            if (!existing.ConsentGrantedAtUtc.HasValue)
            {
                existing.ConsentGrantedAtUtc = DateTime.UtcNow;
            }
        }
        else
        {
            existing.ConsentGrantedAtUtc = null;
            Testimonial.IsPublished = false;
        }

        existing.IsPublished = Testimonial.ConsentGranted && Testimonial.IsPublished;

        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
