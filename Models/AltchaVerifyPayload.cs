namespace SysJaky_N.Models;

/// <summary>
/// Payload used to verify an Altcha proof-of-work solution.
/// </summary>
public class AltchaVerifyPayload
{
    /// <summary>
    /// The original challenge string acting as the proof-of-work seed.
    /// </summary>
    public string Challenge { get; set; } = string.Empty;


    /// <summary>
    /// Required difficulty of the challenge.
    /// </summary>
    public int Difficulty { get; set; }

    /// <summary>
    /// Salt containing expiry information.
    /// </summary>
    public string Salt { get; set; } = string.Empty;

    /// <summary>
    /// Hash algorithm used.
    /// </summary>
    public string Algorithm { get; set; } = "SHA-256";

    /// <summary>
    /// HMAC signature proving the challenge validity.
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// Nonce solving the proof-of-work.
    /// Stored as string to avoid overflow when Altcha produces large values.
    /// </summary>
    public string Nonce { get; set; } = string.Empty;

}

