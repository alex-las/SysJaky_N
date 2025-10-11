using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace SysJaky_N.Pages
{
    public class OfflineModel : PageModel
    {
        private readonly IStringLocalizer<OfflineModel> _localizer;

        public OfflineModel(IStringLocalizer<OfflineModel> localizer)
        {
            _localizer = localizer;
        }

        public void OnGet()
        {
            ViewData["Title"] = _localizer["Title"];
        }
    }
}
