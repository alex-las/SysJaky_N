using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class AdminLocalizationTester
{
    public static IEnumerable<(string Name, Func<Task> Execute)> GetTests()
    {
        yield return ("Course block not-found message is localized", () => VerifyLocalizationAsync<SysJaky_N.Pages.Admin.CourseBlocks.DeleteModel>(
            key: "CourseBlockNotFound",
            expectedCzech: "Požadovaný blok kurzů nebyl nalezen.",
            expectedEnglish: "The requested course block was not found."));

        yield return ("Price schedule validation error is localized", () => VerifyLocalizationAsync<SysJaky_N.Pages.Admin.PriceSchedules.CreateModel>(
            key: "EndTimeMustFollowStart",
            expectedCzech: "Koncový čas musí následovat po začátku.",
            expectedEnglish: "End time must follow the start."));

        yield return ("Course review not-found message is localized", () => VerifyLocalizationAsync<SysJaky_N.Pages.Admin.CourseReviews.IndexModel>(
            key: "CourseReviewNotFound",
            expectedCzech: "Požadovanou recenzi se nepodařilo najít.",
            expectedEnglish: "The requested course review could not be found."));
    }

    private static Task VerifyLocalizationAsync<TModel>(string key, string expectedCzech, string expectedEnglish)
    {
        var localizer = CreateLocalizer(typeof(TModel));
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;

        try
        {
            SetCulture("cs");
            var czechValue = localizer[key];
            EnsureEqual(expectedCzech, czechValue, typeof(TModel), key, "cs");

            SetCulture("en");
            var englishValue = localizer[key];
            EnsureEqual(expectedEnglish, englishValue, typeof(TModel), key, "en");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }

        return Task.CompletedTask;
    }

    private static IStringLocalizer CreateLocalizer(Type resourceType)
    {
        var options = Options.Create(new LocalizationOptions { ResourcesPath = "Resources" });
        var factory = new ResourceManagerStringLocalizerFactory(options, NullLoggerFactory.Instance);
        return factory.Create(resourceType);
    }

    private static void SetCulture(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static void EnsureEqual(string expected, string actual, Type resourceType, string key, string cultureName)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resource '{resourceType.FullName}' with key '{key}' returned '{actual}' for culture '{cultureName}', but '{expected}' was expected.");
        }
    }
}
