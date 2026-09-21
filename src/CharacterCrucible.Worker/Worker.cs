namespace CharacterCrucible.Worker;

/// <summary>
/// Applies advancement requests.
///
/// Most requests never reach a human: the worker evaluates them against the ruleset and
/// applies them. Requests the ruleset marks as gated are routed to a storyteller instead.
///
/// Whatever the path, the worker <b>re-validates before applying</b>. The decision taken at
/// submission may be stale — another request may have landed first, and an escalated request
/// may have sat in a queue for days while the ruleset itself was republished.
/// </summary>
public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker started. No queue subscription yet — that lands in week 3.");

        // Service Bus subscription, outbox draining and idempotent handling go here.
        return Task.CompletedTask;
    }
}
