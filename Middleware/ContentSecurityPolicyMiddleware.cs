using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SysJaky_N.Middleware;

public class ContentSecurityPolicyMiddleware
{
    private const string HeaderName = "Content-Security-Policy";
    private const string PolicyValue =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net; " +
        "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net data:; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'self'; " +
        "object-src 'none';";

    private readonly RequestDelegate _next;

    public ContentSecurityPolicyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
            {
                context.Response.Headers[HeaderName] = PolicyValue;
            }

            return Task.CompletedTask;
        });

        return _next(context);
    }
}
