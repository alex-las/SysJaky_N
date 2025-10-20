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
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<DeleteModel> _localizer;

    public DeleteModel(ApplicationDbContext context, IStringLocalizer<DeleteModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    [BindProperty]
    public Testimonial Testimonial { get; set; } = null!;

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
            return Partial("_DeleteModal", this);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var testimonial = await _context.Testimonials.FirstOrDefaultAsync(t => t.Id == Testimonial.Id);
        if (testimonial == null)
        {
            return NotFound();
        }

        _context.Testimonials.Remove(testimonial);
        await _context.SaveChangesAsync();

        if (IsAjaxRequest())
        {
            TempData["StatusMessage"] = $"Reference \"{testimonial.FullName}\" byla odstraněna.";
            return new JsonResult(new { success = true });
        }

        TempData["StatusMessage"] = $"Reference \"{testimonial.FullName}\" byla odstraněna.";
        return RedirectToPage("Index");
    }

    private bool IsAjaxRequest() => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
