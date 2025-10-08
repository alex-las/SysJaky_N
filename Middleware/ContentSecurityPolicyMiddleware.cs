using System.Collections.Generic;
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
        var connectSources = new List<string>
        {
            "'self'",
            "https://fonts.googleapis.com",
            "https://fonts.gstatic.com",
            "https://cdn.jsdelivr.net"
        };

        if (environment.IsDevelopment())
        {
            connectSources.AddRange(new[]
            {
                "http://localhost:*",
                "https://localhost:*",
                "ws://localhost:*",
                "wss://localhost:*"
            });
        }

        var scriptSources = new[] { "'self'", "'unsafe-inline'" };
        var styleSources = new[] { "'self'", "'unsafe-inline'", "https://fonts.googleapis.com", "https://cdn.jsdelivr.net" };
        var fontSources = new[] { "'self'", "https://fonts.gstatic.com", "https://cdn.jsdelivr.net", "data:" };
        var imageSources = new[] { "'self'", "data:" };

        return string.Join(' ', new[]
        {
            "default-src 'self';",
            $"script-src {string.Join(' ', scriptSources)};",
            $"style-src {string.Join(' ', styleSources)};",
            $"font-src {string.Join(' ', fontSources)};",
            $"img-src {string.Join(' ', imageSources)};",
            $"connect-src {string.Join(' ', connectSources)};",
            "form-action 'self';",
            "frame-ancestors 'self';",
            "object-src 'none';"
        });
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
