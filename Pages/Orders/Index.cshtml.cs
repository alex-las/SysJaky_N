using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SysJaky_N.Pages.Orders;

[Authorize]
public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        return Redirect("/Account/Manage#orders");
    }
}
