using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing;
using SysJaky_N.Controllers;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Pages.Account;
using SysJaky_N.Services;

public static class TestHarness
{
    public static async Task<int> Main()
    {
        var tester = new CourseReminderServiceTester();
        return await tester.RunAsync();
    }
}

internal sealed class CourseReminderServiceTester
{
    public async Task<int> RunAsync()
    {
        var tests = new List<(string Name, Func<Task> Execute)>
        {
            ("Page model localizers resolve resources", RunPageModelLocalizationTestAsync),
            ("Payment service formats localized line items", RunPaymentServiceLocalizationTestAsync),
            ("Course reminders respect different time zones", RunCourseSelectionTestAsync),
            ("Course reminders avoid client-side evaluation warnings", RunClientEvaluationWarningTestAsync),
            ("Analytics dashboard aggregates sales using SQL grouping", RunAnalyticsAggregationTestAsync),
            ("Account pages use localized validation and notifications", RunAccountLocalizationTestAsync)
        };

        tests.AddRange(AdminLocalizationTester.GetTests());

        var success = true;
        foreach (var (name, execute) in tests)
        {
            try
            {
                await execute();
                Console.WriteLine($"[PASS] {name}");
            }
            catch (Exception ex)
            {
                success = false;
                Console.Error.WriteLine($"[FAIL] {name}: {ex}");
            }
        }

        return success ? 0 : 1;
    }

    private static Task RunPageModelLocalizationTestAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IStringLocalizerFactory>();

        var modelTypes = new[]
        {
            typeof(SysJaky_N.Pages.OfflineModel),
            typeof(SysJaky_N.Pages.PrivacyModel),
            typeof(SysJaky_N.Pages.ErrorModel),
            typeof(SysJaky_N.Pages.IndexModel)
        };

        var cultures = new[] { "cs", "en" };

        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            foreach (var cultureName in cultures)
            {
                var culture = new CultureInfo(cultureName);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;

                foreach (var modelType in modelTypes)
                {
                    var localizer = factory.Create(modelType);
                    var title = localizer["Title"];
                    if (title.ResourceNotFound || string.IsNullOrWhiteSpace(title.Value))
                    {
                        throw new InvalidOperationException($"Missing Title resource for {modelType.Name} in culture '{cultureName}'.");
                    }
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        return Task.CompletedTask;
    }

    private static Task RunPaymentServiceLocalizationTestAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IStringLocalizerFactory>();
        var localizer = factory.Create(typeof(PaymentService));

        var cultures = new[] { "cs", "en" };
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            foreach (var cultureName in cultures)
            {
                var culture = new CultureInfo(cultureName);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;

                var lineItem = localizer["LineItemName", 123];
                if (lineItem.ResourceNotFound || string.IsNullOrWhiteSpace(lineItem.Value))
                {
                    throw new InvalidOperationException($"Missing LineItemName resource for culture '{cultureName}'.");
                }

                if (!lineItem.Value.Contains("123", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"LineItemName resource for culture '{cultureName}' does not include the order identifier: '{lineItem.Value}'.");
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        return Task.CompletedTask;
    }

    private static async Task RunCourseSelectionTestAsync()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2024, 5, 10, 6, 0, 0, TimeSpan.Zero));
        var emailSender = new RecordingEmailSender();
        var certificateService = new StubCertificateService();
        var logProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(logProvider);
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var databasePath = Path.Combine(Path.GetTempPath(), $"course-selection-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildServiceProvider(
                databaseName: "CourseSelection",
                timeProvider,
                emailSender,
                certificateService,
                logProvider,
                loggerFactory,
                options => ConfigureSqlite(options, $"Data Source={databasePath};Cache=Shared"));

            await SeedCoursesForSelectionTestAsync(provider);

            var service = new TestableCourseReminderService(new NoopScopeFactory(), loggerFactory.CreateLogger<CourseReminderService>(), timeProvider);
            await service.InvokeExecuteInScopeAsync(provider, CancellationToken.None);

            var recipients = emailSender.SentMessages.Select(m => m.To).OrderBy(e => e).ToArray();
            var expectedRecipients = new[]
            {
                "local@example.com",
                "unspecified@example.com",
                "utc@example.com"
            };

            if (!recipients.SequenceEqual(expectedRecipients))
            {
                throw new InvalidOperationException($"Unexpected recipients: {string.Join(", ", recipients)}");
            }

            if (certificateService.InvocationCount != 1)
            {
                throw new InvalidOperationException($"Certificate service should be invoked once, but was called {certificateService.InvocationCount} times.");
            }

            if (emailSender.SentMessages.Any(m => m.Template != EmailTemplate.CourseReminder))
            {
                throw new InvalidOperationException("Unexpected email template used for course reminders.");
            }
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static async Task RunClientEvaluationWarningTestAsync()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero));
        var emailSender = new RecordingEmailSender();
        var certificateService = new StubCertificateService();
        var logProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(logProvider);
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var evaluationDatabasePath = Path.Combine(Path.GetTempPath(), $"client-evaluation-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildServiceProvider(
                databaseName: "ClientEvaluation",
                timeProvider,
                emailSender,
                certificateService,
                logProvider,
                loggerFactory,
                options => ConfigureSqlite(options, $"Data Source={evaluationDatabasePath};Cache=Shared"));

            await SeedSingleReminderCourseAsync(provider);

            var service = new TestableCourseReminderService(new NoopScopeFactory(), loggerFactory.CreateLogger<CourseReminderService>(), timeProvider);
            await service.InvokeExecuteInScopeAsync(provider, CancellationToken.None);

            var problematicLogs = logProvider.Entries
                .Where(entry => entry.Level >= LogLevel.Warning)
                .Where(entry => entry.Category.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
                .Where(entry => entry.Message.Contains("client", StringComparison.OrdinalIgnoreCase)
                    || entry.Message.Contains("translated", StringComparison.OrdinalIgnoreCase)
                    || entry.Message.Contains("evaluated locally", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (problematicLogs.Count > 0)
            {
                var messages = string.Join(Environment.NewLine, problematicLogs.Select(l => $"[{l.Level}] {l.Category}: {l.Message}"));
                throw new InvalidOperationException($"Client evaluation warnings were logged:{Environment.NewLine}{messages}");
            }
        }
        finally
        {
            if (File.Exists(evaluationDatabasePath))
            {
                File.Delete(evaluationDatabasePath);
            }
        }
    }

    private static async Task RunAnalyticsAggregationTestAsync()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2024, 5, 3, 12, 0, 0, TimeSpan.Zero));
        var emailSender = new RecordingEmailSender();
        var certificateService = new StubCertificateService();
        var logProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(logProvider);
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var databasePath = Path.Combine(Path.GetTempPath(), $"analytics-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildServiceProvider(
                databaseName: "AnalyticsAggregation",
                timeProvider,
                emailSender,
                certificateService,
                logProvider,
                loggerFactory,
                options => ConfigureSqlite(options, $"Data Source={databasePath};Cache=Shared"));

            await SeedAnalyticsDataAsync(provider);

            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var controller = new AnalyticsController(context);

            var result = await controller.GetDashboard(new AnalyticsController.AnalyticsQuery
            {
                From = "2024-05-01",
                To = "2024-05-03"
            }, CancellationToken.None);

            var dashboard = result.Value
                ?? (result.Result as OkObjectResult)?.Value as AnalyticsController.DashboardResponse;

            if (dashboard is null)
            {
                throw new InvalidOperationException("Dashboard response should not be null.");
            }

            var summary = dashboard.Souhrn;

            if (summary.CelkoveTrzby != 350m)
            {
                throw new InvalidOperationException($"Expected total revenue 350, but was {summary.CelkoveTrzby}.");
            }

            if (summary.PredchoziTrzby != 120m)
            {
                throw new InvalidOperationException($"Expected previous revenue 120, but was {summary.PredchoziTrzby}.");
            }

            if (summary.Objednavky != 2)
            {
                throw new InvalidOperationException($"Expected 2 orders, but was {summary.Objednavky}.");
            }

            if (summary.ProdanaMista != 3)
            {
                throw new InvalidOperationException($"Expected seats sold 3, but was {summary.ProdanaMista}.");
            }

            if (summary.AktivniZakaznici != 2 || summary.NoviZakaznici != 1)
            {
                throw new InvalidOperationException($"Unexpected customer counts: active={summary.AktivniZakaznici}, new={summary.NoviZakaznici}.");
            }

            if (summary.PrumernaObjednavka != 175m)
            {
                throw new InvalidOperationException($"Expected average order value 175, but was {summary.PrumernaObjednavka}.");
            }

            if (summary.PrumernaObsazenost != 50d)
            {
                throw new InvalidOperationException($"Expected occupancy percentage 50, but was {summary.PrumernaObsazenost}.");
            }

            var trend = dashboard.Trend.OrderBy(point => point.Datum).ToArray();

            if (trend.Length != 2)
            {
                throw new InvalidOperationException($"Expected 2 trend points, but found {trend.Length}.");
            }

            if (trend[0].Datum != new DateOnly(2024, 5, 1) || trend[0].Trzba != 200m || trend[0].Objednavky != 1)
            {
                throw new InvalidOperationException("Unexpected first trend point.");
            }

            if (trend[1].Datum != new DateOnly(2024, 5, 2) || trend[1].Trzba != 150m || trend[1].Objednavky != 1)
            {
                throw new InvalidOperationException("Unexpected second trend point.");
            }

            var topCourses = dashboard.TopKurzy;

            if (topCourses.Count != 2)
            {
                throw new InvalidOperationException($"Expected 2 top courses, but found {topCourses.Count}.");
            }

            if (topCourses[0].Trzba < topCourses[1].Trzba)
            {
                throw new InvalidOperationException("Top courses should be ordered by revenue.");
            }

            var conversions = dashboard.Konverze;

            if (conversions.Platby != 2 || conversions.Registrace != 3 || conversions.Navstevy != 10)
            {
                throw new InvalidOperationException($"Unexpected conversion metrics: visits={conversions.Navstevy}, registrations={conversions.Registrace}, payments={conversions.Platby}.");
            }
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static async Task RunAccountLocalizationTestAsync()
    {
        using var cultureScope = new CultureScope(new CultureInfo("en"));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddSingleton<IEmailSender, NullEmailSender>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<IUrlHelperFactory, UrlHelperFactory>();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"account-localization-{Guid.NewGuid():N}"));

        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var scopedProvider = scope.ServiceProvider;

        var registerLocalizer = scopedProvider.GetRequiredService<IStringLocalizer<RegisterModel>>();
        var logoutLocalizer = scopedProvider.GetRequiredService<IStringLocalizer<LogoutModel>>();
        var userManager = scopedProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = scopedProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var emailSender = scopedProvider.GetRequiredService<IEmailSender>();
        var dbContext = scopedProvider.GetRequiredService<ApplicationDbContext>();

        var registerHttpContext = new DefaultHttpContext { RequestServices = scopedProvider };
        var registerActionContext = new ActionContext(registerHttpContext, new RouteData(), new PageActionDescriptor());
        var registerModel = new RegisterModel(userManager, signInManager, emailSender, dbContext, registerLocalizer)
        {
            PageContext = new PageContext(registerActionContext),
            Url = new UrlHelper(registerActionContext)
        };

        registerModel.Input = new RegisterModel.InputModel
        {
            Email = "localized.user@example.com",
            Password = "Strong1Password!",
            ConfirmPassword = "Strong1Password!",
            Captcha = "token",
            ReferralCode = "unknown"
        };

        var registerResult = await registerModel.OnPostAsync();
        if (registerResult is not PageResult)
        {
            throw new InvalidOperationException("Registration should stay on the page when the referral code is unknown.");
        }

        var referralErrors = registerModel.ModelState[nameof(RegisterModel.InputModel.ReferralCode)]?.Errors;
        if (referralErrors is null || referralErrors.Count == 0)
        {
            throw new InvalidOperationException("Expected a validation error for the referral code.");
        }

        var expectedReferralMessage = registerLocalizer["ReferralCodeNotFound"].Value;
        var actualReferralMessage = referralErrors[0].ErrorMessage;
        if (!string.Equals(expectedReferralMessage, actualReferralMessage, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected referral message '{expectedReferralMessage}', but was '{actualReferralMessage}'.");
        }

        var logoutHttpContext = new DefaultHttpContext
        {
            RequestServices = scopedProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        var logoutActionContext = new ActionContext(logoutHttpContext, new RouteData(), new PageActionDescriptor());
        var logoutModel = new LogoutModel(signInManager, logoutLocalizer)
        {
            PageContext = new PageContext(logoutActionContext),
            Url = new UrlHelper(logoutActionContext),
            TempData = new TempDataDictionary(logoutHttpContext, new DictionaryTempDataProvider())
        };

        var logoutResult = await logoutModel.OnPostAsync();
        if (logoutResult is not RedirectToPageResult redirect || redirect.PageName != "/Index")
        {
            throw new InvalidOperationException("Logout should redirect to the home page when no return URL is supplied.");
        }

        var expectedLogoutMessage = logoutLocalizer["AlreadySignedOut"].Value;
        var actualLogoutMessage = logoutModel.StatusMessage;
        if (!string.Equals(expectedLogoutMessage, actualLogoutMessage, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected logout message '{expectedLogoutMessage}', but was '{actualLogoutMessage}'.");
        }
    }

    private static async Task SeedCoursesForSelectionTestAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.EnsureCreatedAsync();

        var utcCourse = new Course
        {
            Title = "UTC Course",
            Date = DateTime.SpecifyKind(new DateTime(2024, 5, 13, 9, 0, 0), DateTimeKind.Utc),
            ReminderDays = 3,
            Type = CourseType.Online,
            ReminderMessage = "UTC reminder"
        };

        var localCourse = new Course
        {
            Title = "Local Course",
            Date = DateTime.SpecifyKind(new DateTime(2024, 5, 13, 1, 0, 0), DateTimeKind.Local),
            ReminderDays = 3,
            Type = CourseType.Online,
            ReminderMessage = "Local reminder"
        };

        var unspecifiedCourse = new Course
        {
            Title = "Unspecified Course",
            Date = new DateTime(2024, 5, 13, 23, 30, 0),
            ReminderDays = 3,
            Type = CourseType.Online,
            ReminderMessage = "Unspecified reminder"
        };

        var laterCourse = new Course
        {
            Title = "Later Course",
            Date = new DateTime(2024, 5, 14, 8, 0, 0),
            ReminderDays = 3,
            Type = CourseType.Online
        };

        context.Courses.AddRange(utcCourse, localCourse, unspecifiedCourse, laterCourse);

        var utcUser = new ApplicationUser { Id = Guid.NewGuid().ToString(), Email = "utc@example.com", UserName = "utc@example.com" };
        var localUser = new ApplicationUser { Id = Guid.NewGuid().ToString(), Email = "local@example.com", UserName = "local@example.com" };
        var unspecifiedUser = new ApplicationUser { Id = Guid.NewGuid().ToString(), Email = "unspecified@example.com", UserName = "unspecified@example.com" };

        context.Users.AddRange(utcUser, localUser, unspecifiedUser);
        await context.SaveChangesAsync();

        var utcOrder = new Order
        {
            Status = OrderStatus.Paid,
            UserId = utcUser.Id,
            User = utcUser,
            Items =
            {
                new OrderItem { CourseId = utcCourse.Id }
            }
        };

        var localOrder = new Order
        {
            Status = OrderStatus.Paid,
            UserId = localUser.Id,
            User = localUser,
            Items =
            {
                new OrderItem { CourseId = localCourse.Id }
            }
        };

        var unspecifiedOrder = new Order
        {
            Status = OrderStatus.Paid,
            UserId = unspecifiedUser.Id,
            User = unspecifiedUser,
            Items =
            {
                new OrderItem { CourseId = unspecifiedCourse.Id }
            }
        };

        context.Orders.AddRange(utcOrder, localOrder, unspecifiedOrder);
        await context.SaveChangesAsync();
    }

    private static async Task SeedSingleReminderCourseAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.EnsureCreatedAsync();

        var course = new Course
        {
            Title = "Logging Course",
            Date = new DateTime(2024, 6, 4, 12, 0, 0, DateTimeKind.Utc),
            ReminderDays = 3,
            Type = CourseType.Online
        };

        context.Courses.Add(course);
        await context.SaveChangesAsync();
    }

    private static async Task SeedAnalyticsDataAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.EnsureCreatedAsync();

        var userOne = new ApplicationUser { Id = Guid.NewGuid().ToString(), Email = "alice@example.com", UserName = "alice@example.com" };
        var userTwo = new ApplicationUser { Id = Guid.NewGuid().ToString(), Email = "bob@example.com", UserName = "bob@example.com" };
        var userThree = new ApplicationUser { Id = Guid.NewGuid().ToString(), Email = "carol@example.com", UserName = "carol@example.com" };

        context.Users.AddRange(userOne, userTwo, userThree);

        var courseOne = new Course
        {
            Title = "SQL Foundations",
            Date = new DateTime(2024, 5, 20, 0, 0, 0, DateTimeKind.Utc),
            Price = 100m,
            ReminderDays = 0,
            Type = CourseType.Online
        };

        var courseTwo = new Course
        {
            Title = "Data Insights",
            Date = new DateTime(2024, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            Price = 150m,
            ReminderDays = 0,
            Type = CourseType.Online
        };

        context.Courses.AddRange(courseOne, courseTwo);
        await context.SaveChangesAsync();

        var previousOrder = new Order
        {
            Status = OrderStatus.Paid,
            UserId = userOne.Id,
            User = userOne,
            CreatedAt = new DateTime(2024, 4, 29, 9, 0, 0, DateTimeKind.Utc)
        };
        previousOrder.Items.Add(new OrderItem
        {
            CourseId = courseOne.Id,
            Quantity = 1,
            Total = 120m
        });

        var currentOrderOne = new Order
        {
            Status = OrderStatus.Paid,
            UserId = userOne.Id,
            User = userOne,
            CreatedAt = new DateTime(2024, 5, 1, 10, 0, 0, DateTimeKind.Utc)
        };
        currentOrderOne.Items.Add(new OrderItem
        {
            CourseId = courseOne.Id,
            Quantity = 2,
            Total = 200m
        });

        var currentOrderTwo = new Order
        {
            Status = OrderStatus.Paid,
            UserId = userTwo.Id,
            User = userTwo,
            CreatedAt = new DateTime(2024, 5, 2, 11, 0, 0, DateTimeKind.Utc)
        };
        currentOrderTwo.Items.Add(new OrderItem
        {
            CourseId = courseTwo.Id,
            Quantity = 1,
            Total = 150m
        });

        context.Orders.AddRange(previousOrder, currentOrderOne, currentOrderTwo);
        await context.SaveChangesAsync();

        var termOne = new CourseTerm
        {
            CourseId = courseOne.Id,
            StartUtc = new DateTime(2024, 5, 1, 8, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            Capacity = 10,
            SeatsTaken = 5,
            IsActive = true
        };

        var termTwo = new CourseTerm
        {
            CourseId = courseTwo.Id,
            StartUtc = new DateTime(2024, 5, 2, 8, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2024, 5, 2, 12, 0, 0, DateTimeKind.Utc),
            Capacity = 20,
            SeatsTaken = 10,
            IsActive = true
        };

        context.CourseTerms.AddRange(termOne, termTwo);
        await context.SaveChangesAsync();

        context.WaitlistEntries.AddRange(
            new WaitlistEntry { UserId = userOne.Id, CourseTermId = termOne.Id, CreatedAtUtc = new DateTime(2024, 5, 1, 6, 0, 0, DateTimeKind.Utc) },
            new WaitlistEntry { UserId = userTwo.Id, CourseTermId = termOne.Id, CreatedAtUtc = new DateTime(2024, 5, 1, 7, 0, 0, DateTimeKind.Utc) },
            new WaitlistEntry { UserId = userThree.Id, CourseTermId = termTwo.Id, CreatedAtUtc = new DateTime(2024, 5, 2, 7, 30, 0, DateTimeKind.Utc) }
        );

        var visitStart = new DateTime(2024, 5, 1, 5, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 10; i++)
        {
            context.LogEntries.Add(new SysJaky_N.Models.LogEntry
            {
                Timestamp = visitStart.AddMinutes(i * 30),
                Level = "Information",
                Message = "Visit"
            });
        }

        await context.SaveChangesAsync();
    }

    private static void ConfigureSqlite(DbContextOptionsBuilder options, string connectionString)
    {
        const string typeName = "Microsoft.EntityFrameworkCore.Sqlite.Infrastructure.Internal.SqliteOptionsExtension, Microsoft.EntityFrameworkCore.Sqlite";
        if (Type.GetType(typeName) is not Type extensionType)
        {
            throw new InvalidOperationException($"Unable to resolve type '{typeName}'.");
        }

        var instance = Activator.CreateInstance(extensionType)
            ?? throw new InvalidOperationException("Failed to create SqliteOptionsExtension instance.");

        var withConnectionString = extensionType.GetMethod("WithConnectionString", new[] { typeof(string) })
            ?? throw new InvalidOperationException("WithConnectionString method was not found on SqliteOptionsExtension.");

        var configuredExtension = withConnectionString.Invoke(instance, new object[] { connectionString })
            ?? throw new InvalidOperationException("WithConnectionString returned null.");

        ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension((IDbContextOptionsExtension)configuredExtension);
    }

    private static ServiceProvider BuildServiceProvider(
        string databaseName,
        ManualTimeProvider timeProvider,
        RecordingEmailSender emailSender,
        StubCertificateService certificateService,
        CapturingLoggerProvider logProvider,
        ILoggerFactory loggerFactory,
        Action<DbContextOptionsBuilder>? configureDb = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton(loggerFactory);
        services.AddLogging(builder =>
        {
            builder.AddProvider(logProvider);
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddSingleton<IEmailSender>(emailSender);
        services.AddSingleton<ICertificateService>(certificateService);

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (configureDb is not null)
            {
                configureDb(options);
            }
            else
            {
                options.UseInMemoryDatabase(databaseName);
            }

            options.UseLoggerFactory(loggerFactory);
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });

        return services.BuildServiceProvider();
    }

    private sealed class NullEmailSender : IEmailSender
    {
        public Task SendEmailAsync<TModel>(string to, EmailTemplate template, TModel model, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        private IDictionary<string, object?> _data = new Dictionary<string, object?>();

        public IDictionary<string, object?> LoadTempData(HttpContext context)
            => new Dictionary<string, object?>(_data);

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
            => _data = new Dictionary<string, object?>(values);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUiCulture;

        public CultureScope(CultureInfo culture)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            _originalUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public void SetUtcNow(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class TestableCourseReminderService : CourseReminderService
    {
        public TestableCourseReminderService(IServiceScopeFactory scopeFactory, ILogger<CourseReminderService> logger, TimeProvider timeProvider)
            : base(scopeFactory, logger, timeProvider)
        {
        }

        public Task InvokeExecuteInScopeAsync(IServiceProvider provider, CancellationToken token)
            => ExecuteInScopeAsync(provider, token);
    }

    private sealed class NoopScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
            => throw new NotSupportedException("Scope creation is not supported in the test harness.");
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        private readonly ConcurrentBag<SentEmail> _messages = new();

        public IReadOnlyCollection<SentEmail> SentMessages => _messages;

        public Task SendEmailAsync<TModel>(string to, EmailTemplate template, TModel model, CancellationToken cancellationToken = default)
        {
            _messages.Add(new SentEmail(to, template));
            return Task.CompletedTask;
        }
    }

    private sealed class StubCertificateService : ICertificateService
    {
        public int InvocationCount { get; private set; }

        public Task<int> IssueCertificatesForCompletedEnrollmentsAsync(CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(0);
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentBag<LogEntry> _entries = new();

        public IReadOnlyCollection<LogEntry> Entries => _entries;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

        public void Dispose()
        {
        }
    }

    private sealed record SentEmail(string To, EmailTemplate Template);

    internal sealed record LogEntry(string Category, LogLevel Level, EventId EventId, string Message, Exception? Exception);

    private sealed class CapturingLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly ConcurrentBag<LogEntry> _entries;

        public CapturingLogger(string categoryName, ConcurrentBag<LogEntry> entries)
        {
            _categoryName = categoryName;
            _entries = entries;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _entries.Add(new LogEntry(_categoryName, logLevel, eventId, message, exception));
        }
    }
}
