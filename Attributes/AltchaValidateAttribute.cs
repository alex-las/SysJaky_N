using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using SysJaky_N.Services;

namespace SysJaky_N.Attributes;

public class AltchaValidateAttribute : ActionFilterAttribute
{
    private IStringLocalizer<AltchaValidateAttribute>? _localizer;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (string.Equals(context.HttpContext.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            var form = context.HttpContext.Request.Form;
            var payload = form["altcha"].FirstOrDefault();
            var service = context.HttpContext.RequestServices.GetRequiredService<IAltchaService>();
            _localizer ??= context.HttpContext.RequestServices.GetService<IStringLocalizer<AltchaValidateAttribute>>();
            if (string.IsNullOrEmpty(payload) || !await service.VerifySolutionAsync(payload))
            {
                var localizedMessage = _localizer?["CaptchaFailed"];
                var message = localizedMessage is { ResourceNotFound: false } ? localizedMessage.Value : localizedMessage?.Value;
                context.ModelState.AddModelError("Input.Captcha", string.IsNullOrEmpty(message) ? "Captcha verification failed." : message!);
            }
        }

        await next();
    }
}
