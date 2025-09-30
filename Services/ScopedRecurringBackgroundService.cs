using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SysJaky_N.Services;

public abstract class ScopedRecurringBackgroundService<TService> : BackgroundService where TService : class
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TService> _logger;
    private readonly Func<DateTime, CancellationToken, ValueTask<TimeSpan>> _delayProvider;

    protected ScopedRecurringBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<TService> logger,
        Func<DateTime, CancellationToken, ValueTask<TimeSpan>> delayProvider)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _delayProvider = delayProvider ?? throw new ArgumentNullException(nameof(delayProvider));
    }

    protected abstract Task ExecuteInScopeAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken);

    protected virtual string FailureMessage => "An error occurred while executing the background service.";

    protected virtual string DelayCalculationErrorMessage => "An error occurred while calculating the delay for the background service.";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await ExecuteInScopeAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, FailureMessage);
            }

            TimeSpan delay;
            try
            {
                delay = await _delayProvider(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, DelayCalculationErrorMessage);
                delay = TimeSpan.Zero;
            }

            if (delay <= TimeSpan.Zero)
            {
                continue;
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
