namespace SysJaky_N.Models;

/// <summary>
/// Configuration options for the Altcha service.
/// </summary>
public class AltchaOptions
{
    /// <summary>
    /// Secret key used for signing challenges.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Proof-of-work difficulty (number of leading zeros).
    /// </summary>
    public int Difficulty { get; set; } = 4;

    /// <summary>
    /// Lifetime of a challenge token in seconds.
    /// </summary>
    public int TokenTtlSeconds { get; set; } = 300;
}

