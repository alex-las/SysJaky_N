using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.AuditLogs;

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

    public IList<AuditLog> Logs { get; set; } = new List<AuditLog>();

    public async Task OnGetAsync()
    {
        ViewData["Title"] = _localizer["Title"];
        Logs = await _context.AuditLogs.OrderByDescending(a => a.Timestamp).ToListAsync();
    }
}

