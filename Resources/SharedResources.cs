using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace SysJaky_N.Resources;

/// <summary>
/// Provides strongly-typed accessors for shared localization resources.
/// </summary>
[SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Wrapper around localized resources")]
public static class SharedResources
{
    private static readonly ResourceManager ResourceManager = new(
        "SysJaky_N.Resources.SharedResources",
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
