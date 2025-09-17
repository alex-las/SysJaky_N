using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Companies;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<CompanyProfile> Companies { get; set; } = new();

    [BindProperty]
    public CompanyProfile NewCompany { get; set; } = new();

    public async Task OnGetAsync()
    {
        Companies = await _context.CompanyProfiles.Include(c => c.Manager).ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Companies = await _context.CompanyProfiles.Include(c => c.Manager).ToListAsync();
            return Page();
        }
        _context.CompanyProfiles.Add(NewCompany);
        await _context.SaveChangesAsync();
        return RedirectToPage();
    }
}
