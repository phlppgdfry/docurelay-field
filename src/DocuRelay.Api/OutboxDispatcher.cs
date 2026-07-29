using System.Text.Json;
using DocuRelay.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DocuRelay.Api;

/// <summary>
/// Implements the transactional outbox pattern: persistence succeeds before a transient queue
/// outage is retried. Sending remains at-least-once, which is why the worker is idempotent.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IDocumentQueuePublisher queuePublisher,
    IConfiguration configuration,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(configuration.GetValue("Outbox:PollIntervalSeconds", 1));
        using var timer = new PeriodicTimer(pollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DispatchPendingMessagesAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Unexpected error while dispatching the document outbox.");
            }
        }
    }

    private async Task DispatchPendingMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DocuRelayDbContext>();
        var messages = await database.OutboxMessages
            .Where(message => message.DispatchedAtUtc == null)
            .OrderBy(message => message.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var outboxMessage in messages)
        {
            outboxMessage.AttemptCount++;
            try
            {
                var message = JsonSerializer.Deserialize<DocumentProcessingMessage>(outboxMessage.Payload)
                    ?? throw new InvalidOperationException("Outbox message payload was empty.");
                await queuePublisher.PublishAsync(message, cancellationToken);
                outboxMessage.DispatchedAtUtc = DateTimeOffset.UtcNow;
                outboxMessage.LastError = null;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                outboxMessage.LastError = exception.Message;
                logger.LogWarning(exception, "Could not dispatch outbox message {OutboxMessageId}; it will be retried.", outboxMessage.Id);
            }
        }

        if (messages.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }
    }
}
