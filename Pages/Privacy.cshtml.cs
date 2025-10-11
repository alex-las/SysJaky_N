using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace SysJaky_N.Pages
{
    public class PrivacyModel : PageModel
    {
        private readonly ILogger<PrivacyModel> _logger;
        private readonly IStringLocalizer<PrivacyModel> _localizer;

        public PrivacyModel(ILogger<PrivacyModel> logger, IStringLocalizer<PrivacyModel> localizer)
        {
            _logger = logger;
            _localizer = localizer;
        }

        public void OnGet()
        {
            ViewData["Title"] = _localizer["Title"];
        }
    }

}
