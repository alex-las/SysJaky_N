using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using SysJaky_N.Models;
using SysJaky_N.Attributes;
using SysJaky_N.Services;
using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditService _auditService;

    public LoginModel(SignInManager<ApplicationUser> signInManager, IAuditService auditService)
    {
        _signInManager = signInManager;
        _auditService = auditService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Validation.Required")]
        public string Login { get; set; } = string.Empty;

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
        var loginIdentifier = Input.Login.Trim();

        var user = await _signInManager.UserManager.FindByNameAsync(loginIdentifier)
            ?? await _signInManager.UserManager.FindByEmailAsync(loginIdentifier);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            await _auditService.LogAsync(user.Id, "Login");
            return RedirectToPage("/Index");
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return Page();
    }
}
