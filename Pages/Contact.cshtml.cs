using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SysJaky_N.Resources;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using SysJaky_N.EmailTemplates.Models;

namespace SysJaky_N.Pages;

public class ContactModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly IStringLocalizer<ContactModel> _localizer;

    public ContactModel(ApplicationDbContext context, IEmailSender emailSender, IConfiguration configuration, IStringLocalizer<ContactModel> localizer)
    {
        _context = context;
        _emailSender = emailSender;
        _configuration = configuration;
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Display(Name = nameof(SharedResources.ContactNameLabel), ResourceType = typeof(SharedResources))]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        [StringLength(100, ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.StringLength))]
        public string Name { get; set; } = string.Empty;

        [Display(Name = nameof(SharedResources.ContactEmailLabel), ResourceType = typeof(SharedResources))]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        [EmailAddress(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.EmailAddressInvalid))]
        public string Email { get; set; } = string.Empty;

        [Display(Name = nameof(SharedResources.ContactMessageLabel), ResourceType = typeof(SharedResources))]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        [StringLength(4000, ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.StringLength))]
        public string Message { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var entity = new ContactMessage
        {
            Name = Input.Name,
            Email = Input.Email,
            Message = Input.Message,
            CreatedAt = DateTime.UtcNow
        };

        _context.ContactMessages.Add(entity);
        await _context.SaveChangesAsync();

        var adminEmail = _configuration["SeedAdmin:Email"];
        if (!string.IsNullOrEmpty(adminEmail))
        {
            await _emailSender.SendEmailAsync(
                adminEmail,
                EmailTemplate.ContactMessageNotification,
                new ContactMessageEmailModel(Input.Name, Input.Email, Input.Message));
        }

        TempData["Success"] = _localizer["MessageSent"].Value;
        return RedirectToPage();
    }
}

