using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Models;
using SysJaky_N.Attributes;
using SysJaky_N.Services;
using SysJaky_N.Data;
using System.ComponentModel.DataAnnotations;
using SysJaky_N.EmailTemplates.Models;
using Microsoft.Extensions.Localization;

namespace SysJaky_N.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailSender _emailSender;
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<RegisterModel> _localizer;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailSender emailSender,
        ApplicationDbContext context,
        IStringLocalizer<RegisterModel> localizer)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
        _context = context;
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

        [DataType(DataType.Password)]
        [Display(Name = "Pages.Account.Register.Input.ConfirmPassword.DisplayName")]
        [Compare(nameof(Password), ErrorMessage = "Validation.PasswordMismatch")]
        public string ConfirmPassword { get; set; } = string.Empty;
        public string Captcha { get; set; } = string.Empty;

        [Display(Name = "Pages.Account.Register.Input.ReferralCode.DisplayName")]
        public string? ReferralCode { get; set; }
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
        Company? company = null;

        if (!string.IsNullOrWhiteSpace(Input.ReferralCode))
        {
            var referralCode = Input.ReferralCode.Trim();
            company = await _context.Companies.FirstOrDefaultAsync(c => c.ReferralCode == referralCode);

            if (company == null)
            {
                ModelState.AddModelError(nameof(Input.ReferralCode), _localizer["ReferralCodeNotFound"]);
                return Page();
            }

            Input.ReferralCode = referralCode;
        }

        var user = new ApplicationUser { UserName = Input.Email, Email = Input.Email };
        var result = await _userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            if (company != null)
            {
                _context.CompanyUsers.Add(new CompanyUser
                {
                    CompanyId = company.Id,
                    UserId = user.Id,
                    Role = CompanyRole.Viewer
                });

                await _context.SaveChangesAsync();
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            await _emailSender.SendEmailAsync(
                user.Email!,
                EmailTemplate.Welcome,
                new WelcomeEmailModel(user.Email!, user.Email));
            return RedirectToPage("/Index");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }
}
