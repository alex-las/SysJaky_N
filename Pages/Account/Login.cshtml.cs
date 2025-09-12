using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6)]
        [RegularExpression("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).+$", ErrorMessage = "Password must contain upper and lower case letters and numbers.")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
        public string Captcha { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    [AltchaValidate]
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }
        var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            var user = await _signInManager.UserManager.FindByEmailAsync(Input.Email);
            await _auditService.LogAsync(user?.Id, "Login");
            return RedirectToPage("/Index");
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return Page();
    }
}
