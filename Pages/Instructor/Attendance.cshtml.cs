using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace SysJaky_N.Pages.Instructor;

[Authorize(Policy = AuthorizationPolicies.AdminOrInstructor)]
public class AttendanceModel : PageModel
{
    private readonly IStringLocalizer<AttendanceModel> _localizer;

    public AttendanceModel(IStringLocalizer<AttendanceModel> localizer)
    {
        _localizer = localizer;
    }

    public string CheckInRequestFailedLogMessage => _localizer["CheckInRequestFailedLogMessage"];

    public void OnGet()
    {
    }
}
