using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.AuditLogs;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IList<AuditLog> Logs { get; set; } = new List<AuditLog>();

    public async Task OnGetAsync()
    {
        Logs = await _context.AuditLogs.OrderByDescending(a => a.Timestamp).ToListAsync();
    }
}

