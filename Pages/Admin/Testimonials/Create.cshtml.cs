using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SysJaky_N.Authorization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Testimonials;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<CreateModel> _localizer;

    public CreateModel(ApplicationDbContext context, IStringLocalizer<CreateModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    [BindProperty]
    public Testimonial Testimonial { get; set; } = new();

    public IActionResult OnGet()
    {
        if (IsAjaxRequest())
        {
            return Partial("_CreateModal", this);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Testimonial.IsPublished && !Testimonial.ConsentGranted)
        {
            ModelState.AddModelError(nameof(Testimonial.ConsentGranted), _localizer["ConsentRequired"]);
        }

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Partial("_CreateModal", this);
            }

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

        if (IsAjaxRequest())
        {
            TempData["StatusMessage"] = $"Reference \"{Testimonial.FullName}\" byla vytvořena.";
            return new JsonResult(new { success = true });
        }

        TempData["StatusMessage"] = $"Reference \"{Testimonial.FullName}\" byla vytvořena.";
        return RedirectToPage("Index");
    }

    private bool IsAjaxRequest() => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
