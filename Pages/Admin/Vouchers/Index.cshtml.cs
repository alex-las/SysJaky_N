using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Vouchers;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IList<Voucher> Vouchers { get; set; } = new List<Voucher>();

    public async Task OnGetAsync()
    {
        Vouchers = await _context.Vouchers
            .Include(v => v.AppliesToCourse)
            .OrderBy(v => v.Code)
            .ToListAsync();
    }
}
