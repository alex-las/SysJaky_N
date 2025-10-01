using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models.ViewModels;

namespace SysJaky_N.ViewComponents;

public class ChatbotWidgetViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;

    public ChatbotWidgetViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var settings = await _context.ChatbotSettings
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync();

        if (settings is null || !settings.IsEnabled)
        {
            return new HtmlContentViewComponentResult(new Microsoft.AspNetCore.Html.HtmlString(string.Empty));
        }

        var model = new ChatbotWidgetViewModel
        {
            AutoInitialize = settings.AutoInitialize
        };

        return View("/Pages/Shared/_Chatbot.cshtml", model);
    }
}
