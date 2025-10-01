using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using SysJaky_N.Services.Calendar;
using System.Linq;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Events;
using SysJaky_N.Logging;
using SysJaky_N.Middleware;
using RazorLight;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.IO.Compression;
using System.Security.Claims;
using OfficeOpenXml;
using Microsoft.Extensions.Logging.Abstractions; // kvůli NullLoggerFactory
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Net.Http.Headers;
using Serilog.Extensions.Logging;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

    var builder = WebApplication.CreateBuilder(args);

    if (builder.Environment.IsDevelopment())
    {
        builder.Configuration.AddUserSecrets<Program>(optional: true);
    }

    // --- Connection string ---
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Connection string 'DefaultConnection' not found. Configure it using secrets or KeyVault.");
    }

    var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

    void ConfigureApplicationDbContext(DbContextOptionsBuilder options, bool quietLogging = false)
    {
        options.UseMySql(connectionString, serverVersion);
        if (quietLogging)
        {
            options.UseLoggerFactory(NullLoggerFactory.Instance); // vypne EF logování z tohoto contextu
            options.EnableDetailedErrors(false).EnableSensitiveDataLogging(false);
        }
    }

    // --- Hlavní app DB context ---
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        ConfigureApplicationDbContext(options));

    // --- Tichá továrna pro DB sink (žádné EF logy) ---
    builder.Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(_ =>
        new DelegateDbContextFactory<ApplicationDbContext>(() =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            ConfigureApplicationDbContext(optionsBuilder, quietLogging: true);
            return new ApplicationDbContext(optionsBuilder.Options);
        }));

    // --- Serilog s možností vypnout DB sink přes DISABLE_DB_LOGS=1 ---
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        var disableDbSink = Environment.GetEnvironmentVariable("DISABLE_DB_LOGS") == "1";

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path: "Logs/application-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            // DB sink jen pokud není zakázaný env proměnnou
            .WriteTo.Conditional(_ => !disableDbSink,
                wt => wt.Sink(new EfCoreLogSink(
                    services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                )));
    });

    // Add services to the container.
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationPolicies.AdminOnly,
            policy => policy.RequireRole(ApplicationRoles.Admin));
        options.AddPolicy(AuthorizationPolicies.AdminDashboardAccess,
            policy => policy.RequireRole(ApplicationRoles.AdminDashboardRoles.ToArray()));
        options.AddPolicy(AuthorizationPolicies.AdminOrInstructor,
            policy => policy.RequireRole(ApplicationRoles.Admin, ApplicationRoles.Instructor));
        options.AddPolicy(AuthorizationPolicies.EditorOnly,
            policy => policy.RequireRole(ApplicationRoles.Editor));
        options.AddPolicy(AuthorizationPolicies.CompanyManagerOnly,
            policy => policy.RequireRole(ApplicationRoles.CompanyManager));
        options.AddPolicy(AuthorizationPolicies.StudentCustomer,
            policy => policy.RequireRole(ApplicationRoles.StudentCustomer));
    });

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.None;
    });

    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession();
    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Clear();
        options.Providers.Add<GzipCompressionProvider>();
        options.Providers.Add<BrotliCompressionProvider>();
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
        {
            "text/css",
            "application/javascript",
            "application/json",
            "image/svg+xml"
        });
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    {
        options.Level = CompressionLevel.SmallestSize;
    });
    builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    {
        options.Level = CompressionLevel.SmallestSize;
    });
    builder.Services
        .AddRazorPages()
        .AddViewLocalization()
        .AddDataAnnotationsLocalization(options =>
        {
            options.DataAnnotationLocalizerProvider = (type, factory) =>
                factory.Create(typeof(SysJaky_N.Resources.SharedResources));
        });
    builder.Services.AddControllers();
    builder.Services.Configure<RequestLocalizationOptions>(options =>
    {
        var supportedCultures = new[] { "cs", "en" };
        options.SetDefaultCulture("cs");
        options.AddSupportedCultures(supportedCultures);
        options.AddSupportedUICultures(supportedCultures);

        options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider
        {
            CookieName = CookieRequestCultureProvider.DefaultCookieName
        });
    });
    builder.Services.Configure<PaymentGatewayOptions>(builder.Configuration.GetSection("PaymentGateway"));
    builder.Services.AddScoped<PaymentService>();
    builder.Services.AddSingleton<ICourseSearchOptionProvider, CourseSearchOptionProvider>();
    builder.Services.AddSingleton<IConverter>(sp => new SynchronizedConverter(new PdfTools()));
    builder.Services.AddSingleton<IRazorLightEngine>(sp =>
    {
        var environment = sp.GetRequiredService<IHostEnvironment>();
        var templatesRoot = Path.Combine(environment.ContentRootPath, "EmailTemplates");
        return new RazorLightEngineBuilder()
            .UseFileSystemProject(templatesRoot)
            .UseMemoryCachingProvider()
            .Build();
    });
    builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
    builder.Services.Configure<PushNotificationOptions>(builder.Configuration.GetSection("PushNotifications"));
    builder.Services.AddSingleton<IPushSubscriptionStore, InMemoryPushSubscriptionStore>();
    builder.Services.AddScoped<IEmailSender, EmailSender>();
    builder.Services.Configure<CourseReviewRequestOptions>(builder.Configuration.GetSection("CourseReviews"));
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<ICourseMediaStorage, LocalCourseMediaStorage>();
    builder.Services.AddScoped<ICourseEditor, CourseEditor>();
    builder.Services.AddScoped<CartService>();
    builder.Services.Configure<CertificateOptions>(builder.Configuration.GetSection("Certificates"));
    builder.Services.AddScoped<CertificateService>();
    builder.Services.AddHostedService<CourseReminderService>();
    builder.Services.AddHostedService<SalesStatsService>();
    builder.Services.AddHostedService<CourseReviewRequestService>();
    builder.Services.AddHostedService<WaitlistNotificationService>();
    var waitlistSection = builder.Configuration.GetSection("Waitlist");
    if (!waitlistSection.Exists())
    {
        throw new InvalidOperationException(
            "Konfigurace 'Waitlist' nebyla nalezena. Nastavte Waitlist:PublicBaseUrl a Waitlist:ClaimPath v appsettings nebo uschovaných tajemstvích.");
    }

    var waitlistOptions = waitlistSection.Get<WaitlistOptions>();
    if (waitlistOptions is null || string.IsNullOrWhiteSpace(waitlistOptions.PublicBaseUrl))
    {
        throw new InvalidOperationException(
            "Konfigurace čekací listiny musí obsahovat platnou hodnotu Waitlist:PublicBaseUrl.");
    }

    builder.Services.Configure<WaitlistOptions>(waitlistSection);
    builder.Services.AddSingleton<WaitlistTokenService>();
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ICacheService, CacheService>();
    builder.Services.AddSingleton<IIcsCalendarBuilderFactory, IcsCalendarBuilderFactory>();
    builder.Services.Configure<AltchaOptions>(builder.Configuration.GetSection("Altcha"));
    builder.Services.AddSingleton<IAltchaService, AltchaService>();

    builder.Services.Configure<ForwardedHeadersOptions>(opts =>
    {
        opts.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        opts.KnownNetworks.Clear();
        opts.KnownProxies.Clear();
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("AltchaVerify", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "anon",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));

        options.AddPolicy("Login", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "anon",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));

        options.AddPolicy("Checkout", context =>
        {
            var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var partitionKey = !string.IsNullOrWhiteSpace(userId)
                ? $"user:{userId}"
                : context.Connection.RemoteIpAddress?.ToString() ?? "anon";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 3,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });
    });

    using var bundlerLoggerFactory = new SerilogLoggerFactory(Log.Logger);
    StaticAssetBundler.Build(builder.Environment, bundlerLoggerFactory.CreateLogger(nameof(StaticAssetBundler)));

    var app = builder.Build();

    // DB migrate + seed (bez rekurze – sink používá tichou továrnu ApplicationDbContext)
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();
        var seeder = new AdminSeeder(services);
        await seeder.SeedAsync();
    }

    app.UseForwardedHeaders();

    app.UseExceptionHandler("/Error");

    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var correlation) &&
                correlation is string correlationId)
            {
                diagnosticContext.Set("CorrelationId", correlationId);
            }
        };
    });

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.Use(async (context, next) =>
    {
        var pushFeature = context.Features.Get<IHttpPushPromiseFeature>();
        if (pushFeature != null
            && HttpMethods.IsGet(context.Request.Method)
            && context.Request.Headers.TryGetValue(HeaderNames.Accept, out var accepted)
            && accepted.Any(value => value.Contains("text/html", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                pushFeature.PushPromise("/dist/styles.min.css", new HeaderDictionary
                {
                    [HeaderNames.Accept] = "text/css,*/*;q=0.1"
                });
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex, "HTTP/2 push failed");
            }
        }

        await next();
    });
    app.UseResponseCompression();
    app.UseMiddleware<ContentSecurityPolicyMiddleware>();
    app.UseMiddleware<ImageOptimizationMiddleware>();
    app.UseStaticFiles();

    app.UseRouting();

    var localizationOptions = app.Services
        .GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
    app.UseRequestLocalization(localizationOptions);

    app.UseSession();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapRazorPages();
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller}/{action=Index}/{id?}");
    app.MapControllers();
    // Přesměrování na jednotné místo „Můj účet“
    app.MapGet("/Account/Dashboard", () => Results.Redirect("/Account/Manage", true));
    app.MapGet("/Orders", () => Results.Redirect("/Account/Manage#orders", true));
    app.MapGet("/Orders/Index", () => Results.Redirect("/Account/Manage#orders", true));
    app.MapGet("/Orders/Edit/{id:int}", (int id) => Results.Redirect($"/Admin/Orders/Edit/{id}", true));
    app.MapPost("/payment/webhook", async (HttpRequest request, PaymentService paymentService) =>
    {
        await paymentService.HandleWebhookAsync(request);
        return Results.Ok();
    });

    app.MapGet("/Courses/ICS/{id:int}", async (
        int id,
        ApplicationDbContext context,
        IIcsCalendarBuilderFactory icsCalendarBuilderFactory) =>
    {
        var course = await context.Courses.FindAsync(id);
        if (course == null)
        {
            return Results.NotFound();
        }

        var calendarBuilder = icsCalendarBuilderFactory.Create();
        calendarBuilder.AddEvent(new IcsCalendarEvent($"{course.Id}@sysjaky_n", course.Title, course.Date, course.Date.AddDays(1))
        {
            Description = course.Description,
            Timestamp = DateTime.UtcNow,
            IsAllDay = true
        });

        return calendarBuilder.BuildFile($"{course.Title}.ics");
    });

    app.MapGet("/CourseTerms/ICS/{id:int}", async (
        int id,
        ApplicationDbContext context,
        IIcsCalendarBuilderFactory icsCalendarBuilderFactory) =>
    {
        var term = await context.CourseTerms
            .AsNoTracking()
            .Include(t => t.Course)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (term?.Course == null)
        {
            return Results.NotFound();
        }

        var calendarBuilder = icsCalendarBuilderFactory.Create();
        calendarBuilder.AddEvent(new IcsCalendarEvent($"course-term-{term.Id}@sysjaky_n", term.Course.Title, term.StartUtc, term.EndUtc)
        {
            Description = term.Course.Description,
            Timestamp = DateTime.UtcNow
        });

        var fileName = $"{term.Course.Title}-{term.StartUtc:yyyyMMddHHmm}.ics";
        return calendarBuilder.BuildFile(fileName);
    });

    app.MapGet("/Account/Calendar/MyCourses.ics", async (
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IIcsCalendarBuilderFactory icsCalendarBuilderFactory) =>
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
        {
            return Results.Unauthorized();
        }

        var enrollments = await context.Enrollments
            .AsNoTracking()
            .Where(e => e.UserId == user.Id && e.Status == EnrollmentStatus.Confirmed)
            .Include(e => e.CourseTerm)
                .ThenInclude(term => term.Course)
            .ToListAsync();

        var now = DateTime.UtcNow;

        var calendarBuilder = icsCalendarBuilderFactory.Create("Moje kurzy", includeGregorianCalendar: true);

        foreach (var enrollment in enrollments)
        {
            var term = enrollment.CourseTerm;
            var course = term?.Course;
            if (term == null || course == null)
            {
                continue;
            }

            calendarBuilder.AddEvent(new IcsCalendarEvent($"course-term-{term.Id}-user-{user.Id}@sysjaky_n", course.Title, term.StartUtc, term.EndUtc)
            {
                Description = course.Description,
                Timestamp = now
            });
        }

        return calendarBuilder.BuildFile("moje-kurzy.ics");
    }).RequireAuthorization();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
