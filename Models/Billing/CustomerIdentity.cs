using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models.Billing;

public sealed record CustomerIdentity(
    [property: StringLength(256)] string? Company = null,
    [property: StringLength(256)] string? ContactName = null,
    [property: StringLength(256)] string? Street = null,
    [property: StringLength(100)] string? City = null,
    [property: StringLength(20)] string? PostalCode = null,
    [property: StringLength(100)] string? Country = null,
    [property: StringLength(32)] string? IdentificationNumber = null,
    [property: StringLength(32)] string? TaxNumber = null,
    [property: EmailAddress] string? Email = null,
    [property: Phone] string? Phone = null
);
