using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    // Replace the incorrect usage of `_options` with the correct field `_secretKey`, `_difficulty`, and `_ttlSeconds`
    // These fields are already initialized in the constructor using `IOptions<AltchaOptions>`.

    public bool Verify(AltchaProof proof)
    {
        // 0) sanity
        if (!string.Equals(proof.Algorithm, "SHA-256", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.IsNullOrWhiteSpace(proof.Challenge) || string.IsNullOrWhiteSpace(proof.Salt) || string.IsNullOrWhiteSpace(proof.Signature)) return false;
        if (string.IsNullOrWhiteSpace(proof.Number)) return false;

        // 1) expirace (pokud je v salt)
        try
        {
            var m = Regex.Match(proof.Salt, @"(?:^|[?&])expires=(\d{10,})");
            if (m.Success)
            {
                var unix = long.Parse(m.Groups[1].Value);
                if (DateTimeOffset.FromUnixTimeSeconds(unix) < DateTimeOffset.UtcNow) return false;
            }
        }
        catch { /* ignore */ }

        // 2) podpis (HMAC) – použij difficulty z payloadu (pokud je), jinak konfiguraci
        var difficulty = proof.Difficulty ?? _difficulty;
        var payload = $"{proof.Challenge}:{difficulty}:{proof.Salt}:{proof.Algorithm}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes((proof.Signature ?? "").ToLowerInvariant())))
            return false;

        // 3) proof-of-work – dovol obě varianty (bez dvojtečky i s dvojtečkou) a ověř jak "hex nuly", tak "bitové nuly"
        using var sha = SHA256.Create();

        static bool HasLeadingZeroHex(ReadOnlySpan<byte> hashBytes, int zerosNibbles)
        {
            // zkonvertuj na hex a otestuj prefix "0" * N
            var hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
            return hex.StartsWith(new string('0', zerosNibbles), StringComparison.Ordinal);
        }

        static bool HasLeadingZeroBits(ReadOnlySpan<byte> hashBytes, int zeroBits)
        {
            var bitsLeft = zeroBits;
            foreach (var b in hashBytes)
            {
                if (bitsLeft <= 0) return true;
                var z = b switch
                {
                    0x00 => 8,
                    >= 0x00 and < 0x02 => 7,
                    < 0x04 => 6,
                    < 0x08 => 5,
                    < 0x10 => 4,
                    < 0x20 => 3,
                    < 0x40 => 2,
                    < 0x80 => 1,
                    _ => 0
                };
                if (z < 8)
                    return bitsLeft <= z;
                bitsLeft -= 8;
            }
            return bitsLeft <= 0;
        }

        // a) bez dvojtečky
        var h1 = sha.ComputeHash(Encoding.UTF8.GetBytes(proof.Challenge + proof.Number));
        // b) s dvojtečkou
        var h2 = sha.ComputeHash(Encoding.UTF8.GetBytes($"{proof.Challenge}:{proof.Number}"));

        // Akceptuj, pokud alespoň jedna varianta projde – nejprve "hex nuly", pak "bitové nuly"
        if (HasLeadingZeroHex(h1, difficulty) || HasLeadingZeroHex(h2, difficulty)) return true;

        // Pokud tvůj build Altchy interpretuje difficulty jako "bitové nuly" (nikoli hex),
        // projde jedna z těchto variant:
        if (HasLeadingZeroBits(h1, difficulty) || HasLeadingZeroBits(h2, difficulty)) return true;

        return false;
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
            if (model == null)
            {
                return Task.FromResult(false);
            }

            // Map AltchaVerifyPayload to AltchaProof
            var proof = new AltchaProof
            {
                Challenge = model.Challenge,
                Difficulty = model.Difficulty,
                Salt = model.Salt,
                Algorithm = model.Algorithm,
                Signature = model.Signature,
                Number = (model.Nonce ?? string.Empty).Trim()
            };

            return Task.FromResult(Verify(proof));
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

