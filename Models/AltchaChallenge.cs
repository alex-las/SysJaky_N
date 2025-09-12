namespace SysJaky_N.Models;

public class AltchaChallenge
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string Algorithm { get; set; } = "SHA-256";
    public string Signature { get; set; } = string.Empty;

}

