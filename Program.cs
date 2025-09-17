using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using DinkToPdf;
using DinkToPdf.Contracts;
using System.Text;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using SysJaky_N.Logging;
using SysJaky_N.Middleware;
using RazorLight;
using Microsoft.Extensions.Hosting;
using System.IO;
using OfficeOpenXml;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: "Logs/application-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .WriteTo.Sink(new EfCoreLogSink(services.GetRequiredService<IServiceScopeFactory>()));
    });

    // Add services to the container.
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));

    builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationPolicies.AdminOnly,
            policy => policy.RequireRole(ApplicationRoles.Admin));
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
    builder.Services.AddRazorPages().AddViewLocalization();
    builder.Services.AddControllers();
    builder.Services.Configure<PaymentGatewayOptions>(builder.Configuration.GetSection("PaymentGateway"));
    builder.Services.AddScoped<PaymentService>();
    builder.Services.AddSingleton<IConverter>(new SynchronizedConverter(new PdfTools()));
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
    builder.Services.AddScoped<IEmailSender, EmailSender>();
    builder.Services.Configure<CourseReviewRequestOptions>(builder.Configuration.GetSection("CourseReviews"));
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<ICourseMediaStorage, LocalCourseMediaStorage>();
    builder.Services.AddScoped<CartService>();
    builder.Services.Configure<CertificateOptions>(builder.Configuration.GetSection("Certificates"));
    builder.Services.AddScoped<CertificateService>();
    builder.Services.AddHostedService<CourseReminderService>();
    builder.Services.AddHostedService<SalesStatsService>();
    builder.Services.AddHostedService<CourseReviewRequestService>();
    builder.Services.Configure<DataRetentionOptions>(builder.Configuration.GetSection("DataRetention"));
    builder.Services.AddHostedService<DataRetentionService>();
    builder.Services.Configure<WaitlistOptions>(builder.Configuration.GetSection("Waitlist"));
    builder.Services.AddSingleton<WaitlistTokenService>();
    builder.Services.AddHostedService<WaitlistNotificationService>();
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ICacheService, CacheService>();
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
    });

    var app = builder.Build();

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
    app.UseStaticFiles();

    app.UseRouting();

    var localizationOptions = new RequestLocalizationOptions()
        .SetDefaultCulture("cs")
        .AddSupportedCultures("cs", "en")
        .AddSupportedUICultures("cs", "en");
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
    app.MapPost("/payment/webhook", async (HttpRequest request, PaymentService paymentService) =>
    {
        await paymentService.HandleWebhookAsync(request);
        return Results.Ok();
    });

    static string EscapeIcsText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace(",", "\\,")
            .Replace(";", "\\;");
    }

    static string FormatUtcDateTime(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Unspecified)
        {
            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }
        else
        {
            dateTime = dateTime.ToUniversalTime();
        }

        return dateTime.ToString("yyyyMMddTHHmmssZ");
    }

    app.MapGet("/Courses/ICS/{id:int}", async (int id, ApplicationDbContext context) =>
    {
        var course = await context.Courses.FindAsync(id);
        if (course == null)
        {
            return Results.NotFound();
        }

        var builder = new StringBuilder();
        builder.AppendLine("BEGIN:VCALENDAR");
        builder.AppendLine("VERSION:2.0");
        builder.AppendLine("PRODID:-//SysJaky_N//EN");
        builder.AppendLine("BEGIN:VEVENT");
        builder.AppendLine($"UID:{course.Id}@sysjaky_n");
        builder.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
        builder.AppendLine($"DTSTART;VALUE=DATE:{course.Date:yyyyMMdd}");
        builder.AppendLine($"DTEND;VALUE=DATE:{course.Date.AddDays(1):yyyyMMdd}");
        builder.AppendLine($"SUMMARY:{EscapeIcsText(course.Title)}");
        if (!string.IsNullOrWhiteSpace(course.Description))
        {
            builder.AppendLine($"DESCRIPTION:{EscapeIcsText(course.Description)}");
        }
        builder.AppendLine("END:VEVENT");
        builder.AppendLine("END:VCALENDAR");

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Results.File(bytes, "text/calendar", $"{course.Title}.ics");
    });

    app.MapGet("/CourseTerms/ICS/{id:int}", async (int id, ApplicationDbContext context) =>
    {
        var term = await context.CourseTerms
            .AsNoTracking()
            .Include(t => t.Course)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (term?.Course == null)
        {
            return Results.NotFound();
        }

        var builder = new StringBuilder();
        builder.AppendLine("BEGIN:VCALENDAR");
        builder.AppendLine("VERSION:2.0");
        builder.AppendLine("PRODID:-//SysJaky_N//EN");
        builder.AppendLine("BEGIN:VEVENT");
        builder.AppendLine($"UID:course-term-{term.Id}@sysjaky_n");
        builder.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
        builder.AppendLine($"DTSTART:{FormatUtcDateTime(term.StartUtc)}");
        builder.AppendLine($"DTEND:{FormatUtcDateTime(term.EndUtc)}");
        builder.AppendLine($"SUMMARY:{EscapeIcsText(term.Course.Title)}");
        if (!string.IsNullOrWhiteSpace(term.Course.Description))
        {
            builder.AppendLine($"DESCRIPTION:{EscapeIcsText(term.Course.Description)}");
        }
        builder.AppendLine("END:VEVENT");
        builder.AppendLine("END:VCALENDAR");

        var fileName = $"{term.Course.Title}-{term.StartUtc:yyyyMMddHHmm}.ics";
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Results.File(bytes, "text/calendar", fileName);
    });

    app.MapGet("/Account/Calendar/MyCourses.ics", async (
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context) =>
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

        var builder = new StringBuilder();
        builder.AppendLine("BEGIN:VCALENDAR");
        builder.AppendLine("VERSION:2.0");
        builder.AppendLine("PRODID:-//SysJaky_N//EN");
        builder.AppendLine("CALSCALE:GREGORIAN");
        builder.AppendLine("X-WR-CALNAME:Moje kurzy");

        foreach (var enrollment in enrollments)
        {
            var term = enrollment.CourseTerm;
            var course = term?.Course;
            if (term == null || course == null)
            {
                continue;
            }

            builder.AppendLine("BEGIN:VEVENT");
            builder.AppendLine($"UID:course-term-{term.Id}-user-{user.Id}@sysjaky_n");
            builder.AppendLine($"DTSTAMP:{now:yyyyMMddTHHmmssZ}");
            builder.AppendLine($"DTSTART:{FormatUtcDateTime(term.StartUtc)}");
            builder.AppendLine($"DTEND:{FormatUtcDateTime(term.EndUtc)}");
            builder.AppendLine($"SUMMARY:{EscapeIcsText(course.Title)}");
            if (!string.IsNullOrWhiteSpace(course.Description))
            {
                builder.AppendLine($"DESCRIPTION:{EscapeIcsText(course.Description)}");
            }
            builder.AppendLine("END:VEVENT");
        }

        builder.AppendLine("END:VCALENDAR");

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Results.File(bytes, "text/calendar", "moje-kurzy.ics");
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
