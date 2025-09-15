using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

/// <summary>
/// Service implementing Altcha proof-of-work challenge generation and verification.
/// </summary>
public class AltchaService : IAltchaService
{
    private readonly string _secretKey;
    private readonly int _difficulty;
    private readonly int _ttlSeconds;

    public AltchaService(IOptions<AltchaOptions> options)
    {
        _secretKey = options.Value.SecretKey;
        _difficulty = options.Value.Difficulty;
        _ttlSeconds = options.Value.TokenTtlSeconds;
    }

    public AltchaChallenge CreateChallenge()
    {
        var challenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expires = DateTimeOffset.UtcNow.AddSeconds(_ttlSeconds).ToUnixTimeSeconds();
        var salt = $"{Guid.NewGuid()}?expires={expires}";
        const string algorithm = "SHA-256";
        var data = $"{challenge}:{_difficulty}:{salt}:{algorithm}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();

        return new AltchaChallenge
        {
            Challenge = challenge,
            Difficulty = _difficulty,
            Salt = salt,
            Algorithm = algorithm,
            Signature = signature
        };
    }

    public bool Verify(AltchaVerifyPayload payload)
    {
        if (payload == null)
        {
            return false;
        }

        // Check expiry from salt query string
        var expires = ExtractExpiry(payload.Salt);
        if (expires == null || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
        {
            return false;
        }

        try
        {
            // Validate signature
            var data = $"{payload.Challenge}:{payload.Difficulty}:{payload.Salt}:{payload.Algorithm}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(payload.Signature)))
            {
                return false;
            }

            // Verify proof-of-work
            var input = Encoding.UTF8.GetBytes(payload.Challenge + payload.Nonce);
            var hash = SHA256.HashData(input);
            var hex = Convert.ToHexString(hash).ToLowerInvariant();
            var prefix = new string('0', payload.Difficulty);
            return hex.StartsWith(prefix);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static long? ExtractExpiry(string salt)
    {
        var idx = salt.IndexOf("expires=", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var part = salt[(idx + 8)..];
        return long.TryParse(part, out var val) ? val : null;
    }

    public Task<bool> VerifySolutionAsync(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return Task.FromResult(false);
        }

        try
        {
            var model = JsonSerializer.Deserialize<AltchaVerifyPayload>(payload);
            return Task.FromResult(model != null && Verify(model));
        }
        catch (JsonException)
        {
            return Task.FromResult(false);
        }
        catch (FormatException)
        {
            return Task.FromResult(false);
        }
    }
}

