namespace CharacterCrucible.Worker;

/// <summary>
/// Will apply queued and approved advancement requests, re-validating each first. Not built yet.
/// </summary>
public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker started. No queue subscription yet.");

        // Service Bus subscription, outbox draining and idempotent handling go here.
        return Task.CompletedTask;
    }
}
