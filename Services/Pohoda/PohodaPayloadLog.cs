using System.Diagnostics.CodeAnalysis;

namespace SysJaky_N.Services.Pohoda;

public sealed record PohodaPayloadLog(string? RequestPath, string? ResponsePath)
{
    [MemberNotNullWhen(true, nameof(RequestPath), nameof(ResponsePath))]
    public bool HasData => !string.IsNullOrWhiteSpace(RequestPath) || !string.IsNullOrWhiteSpace(ResponsePath);
}
