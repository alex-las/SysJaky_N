using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace SysJaky_N.Middleware;

public class ContentSecurityPolicyMiddleware
{
    private const string HeaderName = "Content-Security-Policy";

    private readonly RequestDelegate _next;
    private readonly string _policyValue;

    public ContentSecurityPolicyMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _policyValue = BuildPolicyValue(environment);
    }

    private static string BuildPolicyValue(IHostEnvironment environment)
    {
        var connectSources = "'self'";
        if (environment.IsDevelopment())
        {
            connectSources += " http://localhost:* https://localhost:* ws://localhost:* wss://localhost:*";
        }

        return
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net; " +
            "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net data:; " +
            "img-src 'self' data:; " +
            $"connect-src {connectSources}; " +
            "form-action 'self'; " +
            "frame-ancestors 'self'; " +
            "object-src 'none';";
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
            {
                context.Response.Headers[HeaderName] = _policyValue;
            }

            return Task.CompletedTask;
        });

        return _next(context);
    }
}
