using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Companies;

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

    public CompanyProfile? Company { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Company = await _context.CompanyProfiles
            .Include(c => c.Manager)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (Company == null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var company = await _context.CompanyProfiles.FirstOrDefaultAsync(c => c.Id == id);
        if (company == null)
        {
            return NotFound();
        }

        _context.CompanyProfiles.Remove(company);
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = _localizer["CompanyDeleted", company.Name].Value;

        return RedirectToPage("Index");
    }
}
