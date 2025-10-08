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
        static string ReadString(JsonElement source, string propertyName, string fallback = "")
        {
            if (source.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String)
            {
                var value = element.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return fallback;
        }

        static string? ReadNumber(JsonElement source, string propertyName)
        {
            if (!source.TryGetProperty(propertyName, out var element))
            {
                return null;
            }

            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                _ => null
            };
        }

        static int? ReadDifficulty(JsonElement source)
        {
            if (!source.TryGetProperty("difficulty", out var element))
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value))
            {
                return value;
            }

            if (element.ValueKind == JsonValueKind.String &&
                int.TryParse(element.GetString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        var numberText = ReadNumber(proofJson, "number") ?? ReadNumber(proofJson, "nonce");
        var proof = new AltchaProof
        {
            Algorithm = ReadString(proofJson, "algorithm", "SHA-256"),
            Challenge = ReadString(proofJson, "challenge", ReadString(proofJson, "seed")),
            Number = (numberText ?? string.Empty).Trim(),
            Salt = ReadString(proofJson, "salt"),
            Signature = ReadString(proofJson, "signature"),
            Difficulty = ReadDifficulty(proofJson)
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
