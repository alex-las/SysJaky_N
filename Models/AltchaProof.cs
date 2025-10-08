namespace SysJaky_N.Models;

public sealed class AltchaProof
{
    public string Algorithm { get; set; } = "SHA-256";
    public string Challenge { get; set; } = string.Empty; // alias k 'seed'

    // Altcha vrací nonce/number jako celé číslo, které ale může být větší, než zvládne `long`.
    // Proto jej ukládáme jako řetězec a pracujeme s jeho textovou reprezentací.
    public string Number { get; set; } = string.Empty; // alias k 'nonce'

    public string Salt { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public int? Difficulty { get; set; } // <-- musí být nullable
}
