using Microsoft.AspNetCore.Mvc;
using SysJaky_N.Services;

namespace SysJaky_N.Controllers;

[ApiController]
[Route("altcha")]
public class AltchaController : ControllerBase
{
    private readonly IAltchaService _altchaService;

    public AltchaController(IAltchaService altchaService)
    {
        _altchaService = altchaService;
    }

    [HttpGet("challenge")]
    public ActionResult<AltchaChallenge> GetChallenge()
    {
        return _altchaService.CreateChallenge();
    }

    [HttpPost("verify")]
    public ActionResult<object> Verify([FromBody] AltchaVerifyPayload payload)
    {
        var success = _altchaService.Verify(payload);
        return new { success };
    }
}
