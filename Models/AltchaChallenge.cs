namespace SysJaky_N.Models;

/// <summary>
/// Represents a proof-of-work challenge for Altcha verification.
/// </summary>
public class AltchaChallenge
{
    /// <summary>
    /// Random seed used for the proof-of-work calculation.
    /// </summary>
    public string Seed { get; set; } = string.Empty;

    /// <summary>
    /// Required difficulty (number of leading zeros) for the hash.
    /// </summary>
    public int Difficulty { get; set; }

    /// <summary>
    /// Salt containing an expiry query string to prevent replay.
    /// </summary>
    public string Salt { get; set; } = string.Empty;

    /// <summary>
    /// Hash algorithm used for the challenge.
    /// </summary>
    public string Algorithm { get; set; } = "SHA-256";

    /// <summary>
    /// HMAC signature of the challenge parameters.
    /// </summary>
    public string Signature { get; set; } = string.Empty;
}

