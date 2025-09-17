using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SysJaky_N.Pages.Instructor;

[Authorize(Policy = AuthorizationPolicies.AdminOrInstructor)]
public class AttendanceModel : PageModel
{
    public void OnGet()
    {
    }
}
