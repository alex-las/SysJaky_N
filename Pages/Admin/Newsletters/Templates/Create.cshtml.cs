using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Newsletters.Templates;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class CreateModel : PageModel
{
    private const string DefaultBaseLayout = @"<!DOCTYPE html>
<html>
<body style=""font-family: Arial, sans-serif; color: #111827; background-color: #f9fafb; margin: 0; padding: 0;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" role=""presentation"">
        <tr>
            <td align=""center"" style=""padding: 24px 0;"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" role=""presentation"" style=""background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 30px rgba(15, 23, 42, 0.1);"">
                    <tr>
                        <td style=""padding: 32px 40px;"">{{BODY}}</td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

    private readonly ApplicationDbContext _context;

    public CreateModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public void OnGet()
    {
        ViewData["Title"] = "Nová šablona";
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var now = DateTime.UtcNow;
        var template = new NewsletterTemplate
        {
            Name = Input.Name.Trim(),
            PrimaryColor = NormalizeColor(Input.PrimaryColor, "#2563eb"),
            SecondaryColor = NormalizeColor(Input.SecondaryColor, "#facc15"),
            BackgroundColor = NormalizeColor(Input.BackgroundColor, "#f9fafb"),
            BaseLayoutHtml = (Input.BaseLayoutHtml ?? string.Empty).Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _context.NewsletterTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        StatusMessage = "Šablona byla vytvořena.";
        return RedirectToPage("Index");
    }

    private static string NormalizeColor(string? input, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(input) ? fallback : input.Trim();
        return value.StartsWith("#", StringComparison.Ordinal) ? value : "#" + value;
    }

    public sealed class InputModel
    {
        [Required]
        [MaxLength(128)]
        [Display(Name = "Název")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Primární barva")]
        public string PrimaryColor { get; set; } = "#2563eb";

        [Display(Name = "Druhá barva")]
        public string SecondaryColor { get; set; } = "#facc15";

        [Display(Name = "Barva pozadí")]
        public string BackgroundColor { get; set; } = "#f9fafb";

        [Display(Name = "HTML rozložení")]
        public string BaseLayoutHtml { get; set; } = DefaultBaseLayout;
    }
}
