using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace SysJaky_N.Resources;

/// <summary>
/// Provides strongly-typed access to corporate inquiry localization resources.
/// </summary>
[SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Wrapper around localized resources")]
public static class CorporateInquiryResources
{
    private static readonly ResourceManager ResourceManager = new(
        "SysJaky_N.Resources.CorporateInquiryResources",
        typeof(CorporateInquiryResources).GetTypeInfo().Assembly);

    private static string GetString(string name) => ResourceManager.GetString(name) ?? string.Empty;

    public static string ServiceTypeLabel => GetString(nameof(ServiceTypeLabel));
    public static string TrainingTypesLabel => GetString(nameof(TrainingTypesLabel));
    public static string ParticipantCountLabel => GetString(nameof(ParticipantCountLabel));
    public static string PreferredDateLabel => GetString(nameof(PreferredDateLabel));
    public static string ModeLabel => GetString(nameof(ModeLabel));
    public static string TrainingLevelLabel => GetString(nameof(TrainingLevelLabel));
    public static string LocationLabel => GetString(nameof(LocationLabel));
    public static string SpecialRequirementsLabel => GetString(nameof(SpecialRequirementsLabel));
    public static string CompanyIdLabel => GetString(nameof(CompanyIdLabel));
    public static string CompanyNameLabel => GetString(nameof(CompanyNameLabel));
    public static string ContactPersonLabel => GetString(nameof(ContactPersonLabel));
    public static string ContactEmailLabel => GetString(nameof(ContactEmailLabel));
    public static string ContactPhoneLabel => GetString(nameof(ContactPhoneLabel));
    public static string PhoneInvalid => GetString(nameof(PhoneInvalid));
}
