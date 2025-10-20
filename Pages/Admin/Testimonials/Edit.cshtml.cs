using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Authorization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Testimonials;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<EditModel> _localizer;

    public EditModel(ApplicationDbContext context, IStringLocalizer<EditModel> localizer)
    {
        _context = context;
        _localizer = localizer;
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

        if (IsAjaxRequest())
        {
            return Partial("_EditModal", this);
        }

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
            ModelState.AddModelError(nameof(Testimonial.ConsentGranted), _localizer["ConsentRequired"]);
        }

        if (!ModelState.IsValid)
        {
            Testimonial.ConsentGrantedAtUtc = existing.ConsentGrantedAtUtc;
            if (IsAjaxRequest())
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Partial("_EditModal", this);
            }

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

        if (IsAjaxRequest())
        {
            TempData["StatusMessage"] = $"Reference \"{existing.FullName}\" byla aktualizována.";
            return new JsonResult(new { success = true });
        }

        TempData["StatusMessage"] = $"Reference \"{existing.FullName}\" byla aktualizována.";
        return RedirectToPage("Index");
    }

    private bool IsAjaxRequest() => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
