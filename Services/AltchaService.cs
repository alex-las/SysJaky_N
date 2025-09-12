using System.Collections.Concurrent;
using System.Text.Json;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class AltchaService : IAltchaService
{
    private readonly ConcurrentDictionary<string, int> _solutions = new();
    private readonly Random _random = new();

    public AltchaChallenge CreateChallenge()
    {
        var a = _random.Next(1, 10);
        var b = _random.Next(1, 10);
        var id = Guid.NewGuid().ToString("N");
        _solutions[id] = a + b;
        var expires = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var salt = $"{Guid.NewGuid()}?expires={expires}";
        const string algorithm = "SHA-256";
        return new AltchaChallenge { Id = id, Question = $"{a} + {b}", Salt = salt, Algorithm = algorithm };
    }

    public bool Verify(AltchaVerifyPayload payload)
    {
        if (payload == null)
        {
            return false;
        }

        if (_solutions.TryGetValue(payload.Id, out var expected))
        {
            var success = expected == payload.Answer;
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

