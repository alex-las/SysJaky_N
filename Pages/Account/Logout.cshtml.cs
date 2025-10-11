using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStringLocalizer<LogoutModel> _localizer;

    [TempData]
    public string? StatusMessage { get; set; }

    public LogoutModel(SignInManager<ApplicationUser> signInManager, IStringLocalizer<LogoutModel> localizer)
    {
        _signInManager = signInManager;
        _localizer = localizer;
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            StatusMessage = _localizer["AlreadySignedOut"];
            return RedirectToLocal(returnUrl);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated ?? false)
        {
            await _signInManager.SignOutAsync();
            StatusMessage = _localizer["LogoutCompleted"];
        }
        else
        {
            StatusMessage = _localizer["AlreadySignedOut"];
        }

        return RedirectToLocal(returnUrl);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Index");
    }
}
