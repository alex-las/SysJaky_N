using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SysJaky_N.Services;
using SysJaky_N.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

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
    [Consumes("application/json")]
    [EnableRateLimiting("AltchaVerify")]
    [IgnoreAntiforgeryToken]
    public ActionResult<object> Verify([FromBody] JsonElement body) // vezmeme si surové tělo
    {
        _logger.LogInformation("VerifyAttempted {CorrelationId}", HttpContext.TraceIdentifier);

        // 1) Získat JSON proof: buď { payload: "<base64>" } nebo přímo object
        JsonElement proofJson;

        if (body.TryGetProperty("payload", out var payloadEl) && payloadEl.ValueKind == JsonValueKind.String)
        {
            try
            {
                var raw = Convert.FromBase64String(payloadEl.GetString() ?? "");
                using var doc = JsonDocument.Parse(raw);
                proofJson = doc.RootElement.Clone();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid base64 payload");
                return Ok(new { success = false, ok = false, valid = false });
            }
        }
        else
        {
            // payload přišel “naplacato” jako objekt
            proofJson = body;
        }

        // 2) Tolerantní mapování -> AltchaProof
        var proof = new AltchaProof
        {
            Algorithm = proofJson.TryGetProperty("algorithm", out var alg) && alg.ValueKind == JsonValueKind.String ? alg.GetString()! : "SHA-256",
            Challenge = proofJson.TryGetProperty("challenge", out var ch) && ch.ValueKind == JsonValueKind.String ? ch.GetString()! :
                         proofJson.TryGetProperty("seed", out var sd) && sd.ValueKind == JsonValueKind.String ? sd.GetString()! : "",
            Number = proofJson.TryGetProperty("number", out var num) && num.TryGetInt64(out var nVal) ? nVal :
                         proofJson.TryGetProperty("nonce", out var nn) && nn.TryGetInt64(out var n2Val) ? n2Val : 0,
            Salt = proofJson.TryGetProperty("salt", out var salt) && salt.ValueKind == JsonValueKind.String ? salt.GetString()! : "",
            Signature = proofJson.TryGetProperty("signature", out var sign) && sign.ValueKind == JsonValueKind.String ? sign.GetString()! : "",
            Difficulty = proofJson.TryGetProperty("difficulty", out var diff) && diff.TryGetInt32(out var dVal) ? dVal : (int?)null
        };

        // 3) Ověření
        var success = _altchaService.Verify(proof);

        if (!success)
        {
            _logger.LogWarning("VerifyFailed {CorrelationId}", HttpContext.TraceIdentifier);
            return Ok(new { success = false, ok = false, valid = false });
        }

        // Vracíme původní payload (když byl base64) nebo nově vygenerovaný base64 z proofJson
        string payloadOut;
        if (body.TryGetProperty("payload", out var original) && original.ValueKind == JsonValueKind.String)
            payloadOut = original.GetString()!;
        else
            payloadOut = Convert.ToBase64String(Encoding.UTF8.GetBytes(proofJson.GetRawText()));

        _logger.LogInformation("VerifySucceeded {CorrelationId}", HttpContext.TraceIdentifier);
        return Ok(new { success = true, ok = true, valid = true, payload = payloadOut });
    }
}
