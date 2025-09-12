using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace SysJaky_N.Services;

public class AltchaOptions
{
    public string ChallengeRoute { get; set; } = "/altcha/challenge";
    public string VerifyRoute { get; set; } = "/altcha/verify";
    public int TokenTtlSeconds { get; set; } = 300;
    public int Difficulty { get; set; } = 4;
}

public class AltchaChallenge
{
    public string Challenge { get; set; } = string.Empty;
    public int Difficulty { get; set; }
}

public class AltchaVerifyPayload
{
    public string Challenge { get; set; } = string.Empty;
    public long Number { get; set; }
}

public interface IAltchaService
{
    AltchaChallenge CreateChallenge();
    bool Verify(AltchaVerifyPayload payload);
}

public class AltchaService : IAltchaService
{
    private readonly IMemoryCache _cache;
    private readonly AltchaOptions _options;

    public AltchaService(IOptions<AltchaOptions> options, IMemoryCache cache)
    {
        _options = options.Value;
        _cache = cache;
    }

    public AltchaChallenge CreateChallenge()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var token = Convert.ToBase64String(bytes);
        _cache.Set(token, true, TimeSpan.FromSeconds(_options.TokenTtlSeconds));
        return new AltchaChallenge
        {
            Challenge = token,
            Difficulty = _options.Difficulty
        };
    }

    public bool Verify(AltchaVerifyPayload payload)
    {
        if (!_cache.TryGetValue(payload.Challenge, out _))
            return false;

        using var sha = SHA256.Create();
        var input = Encoding.UTF8.GetBytes($"{payload.Challenge}:{payload.Number}");
        var hash = sha.ComputeHash(input);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        var prefix = new string('0', _options.Difficulty);
        var ok = hex.StartsWith(prefix);
        if (ok)
            _cache.Remove(payload.Challenge);
        return ok;
    }
}
