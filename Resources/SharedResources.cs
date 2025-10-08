using System.Reflection;
using System.Resources;

namespace SysJaky_N.Resources;

/// <summary>
/// Provides strongly-typed accessors for shared localization resources.
/// Used in DataAnnotations attributes.
/// </summary>
public static class SharedResources
{
    private static readonly ResourceManager ResourceManager = new(
        "SysJaky_N.Resources.SharedResource",  // DŮLEŽITÉ: Toto odkazuje na marker class (singular)
        typeof(SharedResources).GetTypeInfo().Assembly);

    private static string GetString(string name) => ResourceManager.GetString(name) ?? string.Empty;

    public static string FieldRequired => GetString(nameof(FieldRequired));
    public static string StringLength => GetString(nameof(StringLength));
    public static string Range => GetString(nameof(Range));
    public static string EmailAddressInvalid => GetString(nameof(EmailAddressInvalid));
    public static string ContactNameLabel => GetString(nameof(ContactNameLabel));
    public static string ContactEmailLabel => GetString(nameof(ContactEmailLabel));
    public static string ContactMessageLabel => GetString(nameof(ContactMessageLabel));
}
