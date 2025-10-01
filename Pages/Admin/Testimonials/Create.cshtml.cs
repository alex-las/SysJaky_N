using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SysJaky_N.Authorization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Testimonials;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public CreateModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Testimonial Testimonial { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Testimonial.IsPublished && !Testimonial.ConsentGranted)
        {
            ModelState.AddModelError(nameof(Testimonial.ConsentGranted), "Bez souhlasu nelze referenci publikovat.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Testimonial.ConsentGranted)
        {
            Testimonial.ConsentGrantedAtUtc = DateTime.UtcNow;
        }
        else
        {
            Testimonial.ConsentGrantedAtUtc = null;
            Testimonial.IsPublished = false;
        }

        _context.Testimonials.Add(Testimonial);
        await _context.SaveChangesAsync();

        return RedirectToPage("Index");
    }
}
