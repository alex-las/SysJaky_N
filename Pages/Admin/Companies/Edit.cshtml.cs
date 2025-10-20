using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;
using System.Linq;
using SysJaky_N.Data;

namespace SysJaky_N.Pages.Admin.Companies;

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
    public CompanyFormModel Company { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var company = await _context.CompanyProfiles.FirstOrDefaultAsync(c => c.Id == id);
        if (company == null)
        {
            return NotFound();
        }

        Company = new CompanyFormModel
        {
            Id = company.Id,
            Name = company.Name,
            ReferenceCode = company.ReferenceCode,
            ManagerId = company.ManagerId
        };

        Company.ManagerOptions = await GetManagerOptionsAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Company.ManagerOptions = await GetManagerOptionsAsync();
            return Page();
        }

        if (!Company.Id.HasValue)
        {
            return BadRequest();
        }

        var company = await _context.CompanyProfiles.FirstOrDefaultAsync(c => c.Id == Company.Id);
        if (company == null)
        {
            return NotFound();
        }

        company.Name = Company.Name;
        company.ReferenceCode = Company.ReferenceCode;
        company.ManagerId = Company.ManagerId;

        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = _localizer["CompanyUpdated", company.Name].Value;

        return RedirectToPage("Index");
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
