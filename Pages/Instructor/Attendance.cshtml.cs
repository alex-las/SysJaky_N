using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SysJaky_N.Pages.Instructor;

[Authorize(Roles = "Admin,Instructor")]
public class AttendanceModel : PageModel
{
    public void OnGet()
    {
    }
}
