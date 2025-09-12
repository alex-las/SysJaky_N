using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using SysJaky_N.Services;

namespace SysJaky_N.Attributes;

public class AltchaValidateAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (string.Equals(context.HttpContext.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            var form = context.HttpContext.Request.Form;
            var payload = form["altcha"].FirstOrDefault();
            var service = context.HttpContext.RequestServices.GetRequiredService<IAltchaService>();
            if (string.IsNullOrEmpty(payload) || !await service.VerifySolutionAsync(payload))
            {
                context.ModelState.AddModelError("Input.Captcha", "Captcha verification failed.");
            }
        }

        await next();
    }
}
