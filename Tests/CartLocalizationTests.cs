using System.Globalization;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Pages;
using SysJaky_N.Services;
using Xunit;

namespace SysJaky_N.Tests;

public class CartLocalizationTests
{
    [Theory]
    [InlineData("cs", "Množství musí být větší než nula.")]
    [InlineData("en", "Quantity must be greater than zero.")]
    public async Task AddToCart_InvalidQuantity_ReturnsLocalizedMessage(string cultureName, string expected)
    {
        using var _ = new CultureScope(cultureName);
        await using var context = CreateDbContext();
        var service = CreateCartService(context);
        var session = new TestSession();

        var result = await service.AddToCartAsync(session, 1, 0);

        Assert.False(result.Success);
        Assert.Equal(expected, result.ErrorMessage);
    }

    [Theory]
    [InlineData("cs", "Vybraný kurz již není k dispozici.")]
    [InlineData("en", "Selected course is no longer available.")]
    public async Task AddToCart_MissingCourse_ReturnsLocalizedMessage(string cultureName, string expected)
    {
        using var _ = new CultureScope(cultureName);
        await using var context = CreateDbContext();
        var service = CreateCartService(context);
        var session = new TestSession();

        var result = await service.AddToCartAsync(session, 1, 1);

        Assert.False(result.Success);
        Assert.Equal(expected, result.ErrorMessage);
    }

    [Theory]
    [InlineData("cs", "Některé položky byly z vašeho košíku odebrány, protože již nejsou k dispozici.")]
    [InlineData("en", "Some items were removed from your cart because they are no longer available.")]
    public async Task OnGet_RemovesUnavailableItems_UsesLocalizedMessage(string cultureName, string expected)
    {
        using var _ = new CultureScope(cultureName);
        await using var context = CreateDbContext();
        var cartService = CreateCartService(context);
        var model = CreateCartModel(context, cartService);

        var session = new TestSession();
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature(session));
        model.PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
        {
            HttpContext = httpContext
        };

        cartService.SetItems(session, new[] { new CartItem { CourseId = 42, Quantity = 1 } });

        await model.OnGetAsync();

        Assert.Equal(expected, model.ErrorMessage);
        Assert.Empty(cartService.GetItems(session));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static CartService CreateCartService(ApplicationDbContext context)
    {
        var factory = CreateLocalizerFactory();
        var localizer = new StringLocalizer<CartService>(factory);
        return new CartService(context, localizer);
    }

    private static CartModel CreateCartModel(ApplicationDbContext context, CartService cartService)
    {
        var factory = CreateLocalizerFactory();
        var cartLocalizer = new StringLocalizer<CartModel>(factory);
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object,
            null,
            null,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            null,
            null,
            null,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        var emailSender = new Mock<IEmailSender>();
        var auditService = new Mock<IAuditService>();

        return new CartModel(
            context,
            userManager.Object,
            emailSender.Object,
            auditService.Object,
            cartService,
            cartLocalizer);
    }

    private static ResourceManagerStringLocalizerFactory CreateLocalizerFactory()
    {
        var options = Options.Create(new LocalizationOptions { ResourcesPath = "Resources" });
        return new ResourceManagerStringLocalizerFactory(options, new LoggerFactory());
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUiCulture;

        public CultureScope(string cultureName)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            _originalUiCulture = CultureInfo.CurrentUICulture;

            var culture = new CultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public IEnumerable<string> Keys => _store.Keys;
        public string Id { get; } = Guid.NewGuid().ToString();
        public bool IsAvailable => true;

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value);
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public TestSessionFeature(ISession session)
        {
            Session = session;
        }

        public ISession Session { get; set; }
    }
}
