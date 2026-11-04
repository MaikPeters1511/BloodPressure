using CleanArchitecture.Application.Reminders;

namespace CleanArchitecture.Presentation.API.Infrastructure;

/// <summary>Prüft jede Minute, ob Erinnerungen fällig sind. Der Scope je Durchlauf vermeidet Captive Dependencies.</summary>
public sealed class ReminderWorker(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<ReminderWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Normales Herunterfahren.
        }
    }

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ReminderProcessor>().ProcessDueAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Verarbeitung der Erinnerungen fehlgeschlagen");
        }
    }
}
