using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using SysJaky_N.Models;
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
        var tests = new (string Name, Func<Task> Execute)[]
        {
            ("Course reminders respect different time zones", RunCourseSelectionTestAsync),
            ("Course reminders avoid client-side evaluation warnings", RunClientEvaluationWarningTestAsync)
        };

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
                Console.Error.WriteLine($"[FAIL] {name}: {ex.Message}");
            }
        }

        return success ? 0 : 1;
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

        await using var provider = BuildServiceProvider(
            databaseName: "CourseSelection",
            timeProvider,
            emailSender,
            certificateService,
            logProvider,
            loggerFactory);

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

        await using var provider = BuildServiceProvider(
            databaseName: "ClientEvaluation",
            timeProvider,
            emailSender,
            certificateService,
            logProvider,
            loggerFactory);

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

    private static async Task SeedCoursesForSelectionTestAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

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

    private static ServiceProvider BuildServiceProvider(
        string databaseName,
        ManualTimeProvider timeProvider,
        RecordingEmailSender emailSender,
        StubCertificateService certificateService,
        CapturingLoggerProvider logProvider,
        ILoggerFactory loggerFactory)
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
            options.UseInMemoryDatabase(databaseName);
            options.UseLoggerFactory(loggerFactory);
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });

        return services.BuildServiceProvider();
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
