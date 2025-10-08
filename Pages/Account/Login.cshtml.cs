using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using SysJaky_N.Models;
using SysJaky_N.Attributes;
using SysJaky_N.Services;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditService _auditService;
    private readonly IStringLocalizer<LoginModel> _localizer;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        IAuditService auditService,
        IStringLocalizer<LoginModel> localizer)
    {
        _signInManager = signInManager;
        _auditService = auditService;
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Validation.Required")]
        [EmailAddress(ErrorMessage = "Validation.EmailAddress")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Validation.Required")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Validation.StringLengthRange")]
        [RegularExpression("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).+$", ErrorMessage = "Validation.PasswordComplexity")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
        public string Captcha { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    [EnableRateLimiting("Login")]
    [AltchaValidate]
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }
        var email = Input.Email.Trim();
        Input.Email = email;

        var user = await _signInManager.UserManager.FindByEmailAsync(email);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, _localizer["InvalidLogin"]);
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: false);
        if (result.Succeeded)
        {
            await _auditService.LogAsync(user.Id, "Login");
            return RedirectToPage("/Index");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, _localizer["AccountLocked"]);
            return Page();
        }

        if (result.IsNotAllowed)
        {
            if (!await _signInManager.UserManager.IsEmailConfirmedAsync(user))
            {
                ModelState.AddModelError(string.Empty, _localizer["EmailNotConfirmed"]);
            }
            else
            {
                ModelState.AddModelError(string.Empty, _localizer["LoginNotAllowed"]);
            }

            return Page();
        }

        ModelState.AddModelError(string.Empty, _localizer["InvalidLogin"]);
        return Page();
    }
}
