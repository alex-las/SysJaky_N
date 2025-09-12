using System.Text.Json;

namespace SysJaky_N.Services;

public class AltchaService : IAltchaService
{
    public Task<bool> VerifySolutionAsync(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return Task.FromResult(false);
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            // Basic check: ensure solution field exists
            return Task.FromResult(doc.RootElement.TryGetProperty("s", out _));
        }
        catch (JsonException)
        {
            return Task.FromResult(false);
        }

    }
}
