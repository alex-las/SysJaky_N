using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class AltchaService : IAltchaService
{
    private readonly ConcurrentDictionary<string, (int Answer, DateTime Expires)> _solutions = new();
    private readonly Random _random = new();
    private readonly string _secretKey;

    public AltchaService(IOptions<AltchaOptions> options)
    {
        _secretKey = options.Value.SecretKey;
    }

    public AltchaChallenge CreateChallenge()
    {
        var a = RandomNumberGenerator.GetInt32(1, 10);
        var b = RandomNumberGenerator.GetInt32(1, 10);
        var id = Guid.NewGuid().ToString("N");
        var expiresAt = DateTime.UtcNow.AddMinutes(5);
        _solutions[id] = (a + b, expiresAt);
        var expires = new DateTimeOffset(expiresAt).ToUnixTimeSeconds();
        var salt = $"{Guid.NewGuid()}?expires={expires}";
        const string algorithm = "SHA-256";
        var challenge = new AltchaChallenge { Id = id, Question = $"{a} + {b}", Salt = salt, Algorithm = algorithm };
        var data = $"{challenge.Id}:{challenge.Question}:{challenge.Salt}:{challenge.Algorithm}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
        challenge.Signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
        return challenge;
    }

    public bool Verify(AltchaVerifyPayload payload)
    {
        if (payload == null)
        {
            return false;
        }

        // remove expired challenges
        var now = DateTime.UtcNow;
        foreach (var kv in _solutions.ToArray())
        {
            if (kv.Value.Expires <= now)
            {
                _solutions.TryRemove(kv.Key, out _);
            }
        }

        if (_solutions.TryGetValue(payload.Id, out var expected))
        {
            if (expected.Expires <= now)
            {
                _solutions.TryRemove(payload.Id, out _);
                return false;
            }

            var success = expected.Answer == payload.Answer;
            if (success)
            {
                _solutions.TryRemove(payload.Id, out _);
            }
            return success;
        }

        return false;
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
            if (model == null)
            {
                return Task.FromResult(false);
            }
            return Task.FromResult(Verify(model));
        }
        catch (JsonException)
        {
            return Task.FromResult(false);
        }
    }
}

