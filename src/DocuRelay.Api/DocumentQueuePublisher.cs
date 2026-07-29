using System.Text.Json;
using System.Threading.Channels;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using DocuRelay.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DocuRelay.Api;

public interface IDocumentQueuePublisher
{
    Task PublishAsync(DocumentProcessingMessage message, CancellationToken cancellationToken);
}

public sealed class ServiceBusDocumentQueuePublisher(ServiceBusClient client, IConfiguration configuration) : IDocumentQueuePublisher, IAsyncDisposable
{
    private readonly ServiceBusSender _sender = client.CreateSender(configuration["ServiceBus:QueueName"] ?? "document-processing");

    public async Task PublishAsync(DocumentProcessingMessage message, CancellationToken cancellationToken)
    {
        var serviceBusMessage = new ServiceBusMessage(JsonSerializer.Serialize(message))
        {
            MessageId = message.DocumentId.ToString("N"),
            CorrelationId = message.CorrelationId,
            ContentType = "application/json",
            Subject = "DocumentProcessingRequested"
        };
        await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}

/// <summary>In-process queue used when no Azure Service Bus namespace is configured.</summary>
public sealed class DevelopmentDocumentQueuePublisher : IDocumentQueuePublisher
{
    private readonly Channel<DocumentProcessingMessage> _channel = Channel.CreateUnbounded<DocumentProcessingMessage>();

    public Task PublishAsync(DocumentProcessingMessage message, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(message, cancellationToken).AsTask();

    public IAsyncEnumerable<DocumentProcessingMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>Shared, idempotent document processing logic for local queue processing.</summary>
public sealed class LocalDocumentProcessor(IServiceScopeFactory scopeFactory, ILogger<LocalDocumentProcessor> logger)
{
    public async Task ProcessAsync(DocumentProcessingMessage message, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DocuRelayDbContext>();
        var document = await database.Documents.SingleOrDefaultAsync(item => item.Id == message.DocumentId, cancellationToken);
        if (document is null)
        {
            logger.LogWarning("Ignoring local document-processing message {MessageId}: document {DocumentId} does not exist.", message.CorrelationId, message.DocumentId);
            return;
        }

        if (document.ProcessingStatus == "Completed")
        {
            logger.LogInformation("Ignoring duplicate local message for completed document {DocumentId}.", document.Id);
            return;
        }

        document.ProcessingStatus = "Processing";
        document.LastError = null;
        await database.SaveChangesAsync(cancellationToken);

        // Represents OCR, malware scanning, or metadata extraction in a deterministic local demo.
        await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken);
        document.ProcessingStatus = "Completed";
        document.ProcessedAtUtc = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Local worker completed document {DocumentId} with correlation {CorrelationId}.", document.Id, message.CorrelationId);
    }
}

public sealed class DevelopmentDocumentProcessor(
    DevelopmentDocumentQueuePublisher queue,
    LocalDocumentProcessor processor,
    ILogger<DevelopmentDocumentProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await processor.ProcessAsync(message, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Local worker failed document {DocumentId}.", message.DocumentId);
            }
        }
    }
}

public static class ServiceBusRegistration
{
    public static IServiceCollection AddDocumentQueue(this IServiceCollection services, IConfiguration configuration)
    {
        var namespaceName = configuration["ServiceBus:FullyQualifiedNamespace"];
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            services.AddSingleton<DevelopmentDocumentQueuePublisher>();
            services.AddSingleton<IDocumentQueuePublisher>(serviceProvider => serviceProvider.GetRequiredService<DevelopmentDocumentQueuePublisher>());
            services.AddSingleton<LocalDocumentProcessor>();
            services.AddHostedService<DevelopmentDocumentProcessor>();
            return services;
        }

        services.AddSingleton(new ServiceBusClient(namespaceName, new DefaultAzureCredential()));
        services.AddSingleton<IDocumentQueuePublisher, ServiceBusDocumentQueuePublisher>();
        return services;
    }
}
