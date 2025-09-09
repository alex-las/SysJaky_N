using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.DiscountCodes;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IList<DiscountCode> DiscountCodes { get; set; } = new List<DiscountCode>();

    public async Task OnGetAsync()
    {
        DiscountCodes = await _context.DiscountCodes.OrderBy(d => d.Code).ToListAsync();
    }
}
