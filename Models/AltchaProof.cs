namespace SysJaky_N.Models;

public sealed class AltchaProof
{
    public string Algorithm { get; set; } = "SHA-256";
    public string Challenge { get; set; } = string.Empty; // alias k 'seed'
    public long Number { get; set; }                 // alias k 'nonce'
    public string Salt { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public int? Difficulty { get; set; }                 // <-- musí být nullable
}
