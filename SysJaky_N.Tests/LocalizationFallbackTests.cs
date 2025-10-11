using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SysJaky_N.EmailTemplates.Models;
using SysJaky_N.Services;
using Xunit;

namespace SysJaky_N.Tests;

public class LocalizationFallbackTests
{
    [Theory]
    [InlineData("cs", "Termín #1")]
    [InlineData("en", "Term #1")]
    public async Task WaitlistNotificationService_SendsEmailWithLocalizedFallback(string cultureName, string expectedTitle)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;

        try
        {
            var culture = new CultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            using var provider = new ServiceCollection().BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var dataProtectionProvider = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
            var tokenService = new WaitlistTokenService(dataProtectionProvider, NullLogger<WaitlistTokenService>.Instance);
            var service = new WaitlistNotificationService(
                scopeFactory,
                tokenService,
                Options.Create(new WaitlistOptions { PublicBaseUrl = "https://example.test" }),
                NullLogger<WaitlistNotificationService>.Instance,
                CreateLocalizer<WaitlistNotificationService>());

            var fakeEmailSender = new FakeEmailSender();
            var fallbackTitle = service.ResolveCourseTitle(null, 1);

            await fakeEmailSender.SendEmailAsync(
                "waitlist@example.com",
                EmailTemplate.WaitlistSeatAvailable,
                new WaitlistSeatAvailableEmailModel(
                    fallbackTitle,
                    "https://example.test/waitlist",
                    DateTime.UtcNow.AddHours(1),
                    24));

            var message = Assert.Single(fakeEmailSender.SentMessages);
            Assert.Equal(EmailTemplate.WaitlistSeatAvailable, message.Template);
            var model = Assert.IsType<WaitlistSeatAvailableEmailModel>(message.Model);
            Assert.Equal(expectedTitle, model.CourseTitle);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    [Theory]
    [InlineData("cs", "Termín #2")]
    [InlineData("en", "Term #2")]
    public async Task CourseReviewRequestService_SendsEmailWithLocalizedFallback(string cultureName, string expectedTitle)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;

        try
        {
            var culture = new CultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            using var provider = new ServiceCollection().BuildServiceProvider();
            var service = new CourseReviewRequestService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new CourseReviewRequestOptions { PublicBaseUrl = "https://example.test" }),
                NullLogger<CourseReviewRequestService>.Instance,
                CreateLocalizer<CourseReviewRequestService>());

            var fakeEmailSender = new FakeEmailSender();
            var fallbackTitle = service.ResolveCourseTitle(string.Empty, 2);

            await fakeEmailSender.SendEmailAsync(
                "review@example.com",
                EmailTemplate.CourseReviewRequest,
                new CourseReviewRequestEmailModel(fallbackTitle, "https://example.test/review/2"));

            var message = Assert.Single(fakeEmailSender.SentMessages);
            Assert.Equal(EmailTemplate.CourseReviewRequest, message.Template);
            var model = Assert.IsType<CourseReviewRequestEmailModel>(message.Model);
            Assert.Equal(expectedTitle, model.CourseTitle);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    private static IStringLocalizer<T> CreateLocalizer<T>()
    {
        var localizationOptions = Options.Create(new LocalizationOptions { ResourcesPath = "Resources" });
        var factory = new ResourceManagerStringLocalizerFactory(localizationOptions, NullLoggerFactory.Instance);
        return new TypedStringLocalizer<T>(factory.Create(typeof(T)));
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        private readonly List<SentEmail> _messages = new();

        public IReadOnlyList<SentEmail> SentMessages => _messages;

        public Task SendEmailAsync<TModel>(string to, EmailTemplate template, TModel model, CancellationToken cancellationToken = default)
        {
            _messages.Add(new SentEmail(to, template, model!));
            return Task.CompletedTask;
        }
    }

    private sealed record SentEmail(string To, EmailTemplate Template, object Model);

    private sealed class TypedStringLocalizer<T> : IStringLocalizer<T>
    {
        private readonly IStringLocalizer _inner;

        public TypedStringLocalizer(IStringLocalizer inner)
        {
            _inner = inner;
        }

        public LocalizedString this[string name] => _inner[name];

        public LocalizedString this[string name, params object[] arguments] => _inner[name, arguments];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => _inner.GetAllStrings(includeParentCultures);

        public IStringLocalizer WithCulture(CultureInfo culture)
            => _inner;
    }
}
