// Models/AltchaVerifyRequest.cs
namespace SysJaky_N.Models;

public sealed class AltchaVerifyRequest
{
    public string? Payload { get; set; }  // base64 JSON z widgetu
    public string? Code { get; set; }  // nepovinné (pro jiné módy)
}
