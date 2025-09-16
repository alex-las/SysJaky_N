using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SysJaky_N.Middleware;

namespace SysJaky_N.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        public string? RequestId { get; private set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        private readonly ILogger<ErrorModel> _logger;

        public ErrorModel(ILogger<ErrorModel> logger)
        {
            _logger = logger;
        }

        public void OnGet(string? correlationId)
        {
            RequestId = ResolveCorrelationId(correlationId);
            if (!string.IsNullOrEmpty(RequestId))
            {
                _logger.LogInformation("Rendering error page for correlation {CorrelationId}", RequestId);
            }
        }

        private string ResolveCorrelationId(string? correlationId)
        {
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                return correlationId;
            }

            if (HttpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value) &&
                value is string existing &&
                !string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            return Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        }
    }

}
