using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SysJaky_N.Services;
using Xunit;

namespace SysJaky_N.Tests;

public class ScopedRecurringBackgroundServiceTests
{
    [Fact]
    public async Task LogsErrorAndRetriesAfterFailure()
    {
        var scope = new Mock<IServiceScope>();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        scope.SetupGet(s => s.ServiceProvider).Returns(serviceProvider);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory
            .Setup(s => s.CreateScope())
            .Returns(scope.Object);

        var logger = new Mock<ILogger<TestRecurringService>>();

        var firstRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var service = new TestRecurringService(
            scopeFactory.Object,
            logger.Object,
            async (iteration, _, _) =>
            {
                if (iteration == 1)
                {
                    firstRun.TrySetResult();
                    throw new InvalidOperationException("first iteration failed");
                }

                secondRun.TrySetResult();
                await Task.Yield();
            });

        await service.StartAsync(CancellationToken.None);

        await firstRun.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await secondRun.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await service.StopAsync(CancellationToken.None);

        logger.VerifyLog(LogLevel.Error, "Test failure", Times.Once());
        scopeFactory.Verify(s => s.CreateScope(), Times.AtLeast(2));
    }

    private sealed class TestRecurringService : ScopedRecurringBackgroundService<TestRecurringService>
    {
        private readonly Func<int, IServiceProvider, CancellationToken, Task> _run;
        private int _iteration;

        public TestRecurringService(
            IServiceScopeFactory scopeFactory,
            ILogger<TestRecurringService> logger,
            Func<int, IServiceProvider, CancellationToken, Task> run)
            : base(scopeFactory, logger, (_, _) => ValueTask.FromResult(TimeSpan.Zero))
        {
            _run = run;
        }

        protected override string FailureMessage => "Test failure";

        protected override Task ExecuteInScopeAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
        {
            var iteration = Interlocked.Increment(ref _iteration);
            return _run(iteration, serviceProvider, stoppingToken);
        }
    }
}

internal static class LoggerMockExtensions
{
    public static void VerifyLog<T>(this Mock<ILogger<T>> loggerMock, LogLevel level, string message, Times times)
    {
        loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString() == message),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
