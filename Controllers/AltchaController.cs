using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SysJaky_N.Services;
using Microsoft.Extensions.Logging;

namespace SysJaky_N.Controllers;

[ApiController]
[Route("altcha")]
public class AltchaController : ControllerBase
{
    private readonly IAltchaService _altchaService;
    private readonly ILogger<AltchaController> _logger;

    public AltchaController(IAltchaService altchaService, ILogger<AltchaController> logger)
    {
        _altchaService = altchaService;
        _logger = logger;
    }

    [HttpGet("challenge")]
    public ActionResult<AltchaChallenge> GetChallenge()
    {
        var correlationId = HttpContext.TraceIdentifier;
        _logger.LogInformation("ChallengeIssued {CorrelationId}", correlationId);
        return _altchaService.CreateChallenge();
    }

    [HttpPost("verify")]
    [EnableRateLimiting("AltchaVerify")]
    public ActionResult<object> Verify([FromBody] AltchaVerifyPayload payload)
    {
        var correlationId = HttpContext.TraceIdentifier;
        _logger.LogInformation("VerifyAttempted {CorrelationId}", correlationId);
        var success = _altchaService.Verify(payload);
        if (success)
        {
            _logger.LogInformation("VerifySucceeded {CorrelationId}", correlationId);
        }
        else
        {
            _logger.LogWarning("VerifyFailed {CorrelationId}", correlationId);
        }
        return new { success };
    }
}
