using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Vouchers;

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

    public IList<Voucher> Vouchers { get; set; } = new List<Voucher>();

    public async Task OnGetAsync()
    {
        ViewData["Title"] = _localizer["Title"];
        Vouchers = await _context.Vouchers
            .AsNoTracking()
            .Include(v => v.AppliesToCourse)
            .OrderBy(v => v.Code)
            .ToListAsync();
    }
}
