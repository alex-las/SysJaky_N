using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models.Billing;

public sealed record CustomerIdentity(
    [property: StringLength(255)] string? Company,
    [property: StringLength(255)] string? Name,
    [property: StringLength(255)] string? Street,
    [property: StringLength(255)] string? City,
    [property: StringLength(32)] string? Zip,
    [property: StringLength(64)] string? Country)
{
}
