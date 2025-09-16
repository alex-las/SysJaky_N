using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    public ContactModel(ApplicationDbContext context, IEmailSender emailSender, IConfiguration configuration)
    {
        _context = context;
        _emailSender = emailSender;
        _configuration = configuration;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(4000)]
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

        TempData["Success"] = "Message sent.";
        return RedirectToPage();
    }
}

