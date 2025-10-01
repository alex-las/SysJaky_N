using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Authorization;
using SysJaky_N.Data;

namespace SysJaky_N.Pages.Admin.Dashboard;

[Authorize(Policy = AuthorizationPolicies.AdminDashboardAccess)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public ChatbotSettingsInput ChatbotSettings { get; set; } = new();

    public async Task OnGetAsync()
    {
        var settings = await _context.ChatbotSettings
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync();

        if (settings is null)
        {
            ChatbotSettings.IsEnabled = true;
            ChatbotSettings.AutoInitialize = true;
        }
        else
        {
            ChatbotSettings.IsEnabled = settings.IsEnabled;
            ChatbotSettings.AutoInitialize = settings.AutoInitialize;
        }
    }

    public async Task<IActionResult> OnPostChatbotAsync()
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        var settings = await _context.ChatbotSettings
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync();

        if (settings is null)
        {
            settings = new Models.ChatbotSettings();
            _context.ChatbotSettings.Add(settings);
        }

        settings.IsEnabled = ChatbotSettings.IsEnabled;
        settings.AutoInitialize = ChatbotSettings.AutoInitialize;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["ChatbotSettingsSaved"] = true;

        return RedirectToPage();
    }

    public class ChatbotSettingsInput
    {
        public bool IsEnabled { get; set; }
        public bool AutoInitialize { get; set; }
    }
}
