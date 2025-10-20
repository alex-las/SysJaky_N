using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;
using System.Linq;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Companies;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<IndexModel> _localizer;

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public List<CompanyProfile> Companies { get; set; } = new();

    [BindProperty]
    public CompanyFormModel NewCompany { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadPageDataAsync();
            return Page();
        }

        var company = new CompanyProfile
        {
            Name = NewCompany.Name,
            ReferenceCode = NewCompany.ReferenceCode,
            ManagerId = NewCompany.ManagerId
        };

        _context.CompanyProfiles.Add(company);
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = _localizer["CompanyCreated", company.Name].Value;

        return RedirectToPage();
    }

    private async Task LoadPageDataAsync()
    {
        Companies = await _context.CompanyProfiles
            .Include(c => c.Manager)
            .OrderBy(c => c.Name)
            .ToListAsync();

        NewCompany.ManagerOptions = await GetManagerOptionsAsync();
    }

    private async Task<IEnumerable<SelectListItem>> GetManagerOptionsAsync()
    {
        var users = await _context.Users
            .OrderBy(u => u.Email)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.UserName
            })
            .ToListAsync();

        return users.Select(u => new SelectListItem
        {
            Value = u.Id,
            Text = string.IsNullOrEmpty(u.Email) ? (u.UserName ?? u.Id) : u.Email
        });
    }
}
