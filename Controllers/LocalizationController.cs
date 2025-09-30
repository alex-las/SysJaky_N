using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace SysJaky_N.Controllers;

[Route("[controller]/[action]")]
public class LocalizationController : Controller
{
    private readonly RequestLocalizationOptions _localizationOptions;

    public LocalizationController(IOptions<RequestLocalizationOptions> options)
    {
        _localizationOptions = options.Value;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return BadRequest();
        }

        var supportedCultures = _localizationOptions.SupportedCultures?.Select(c => c.Name).ToHashSet()
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!supportedCultures.Contains(culture))
        {
            return BadRequest();
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = false,
                Secure = Request.IsHttps
            });

        if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            returnUrl = Url.Content("~/");
        }

        return LocalRedirect(returnUrl);
    }
}
