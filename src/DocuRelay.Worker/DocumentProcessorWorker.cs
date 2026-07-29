using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DocuRelay.Contracts;
using Microsoft.Data.SqlClient;

namespace DocuRelay.Worker;

public sealed class DocumentProcessorWorker(
    IConfiguration configuration,
    ILogger<DocumentProcessorWorker> logger,
    IServiceProvider services) : BackgroundService
{
    private const string Completed = "Completed";
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = services.GetService<ServiceBusClient>();
        if (client is null)
        {
            logger.LogWarning("ServiceBus:FullyQualifiedNamespace is not configured. The worker is idle.");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        _processor = client.CreateProcessor(configuration["ServiceBus:QueueName"] ?? "document-processing", new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 4,
            PrefetchCount = 8
        });
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
        logger.LogInformation("Document processor started.");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        DocumentProcessingMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<DocumentProcessingMessage>(args.Message.Body);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Dead-lettering invalid message {MessageId}.", args.Message.MessageId);
            await args.DeadLetterMessageAsync(args.Message, "InvalidDocumentMessage", "The body is not a valid document-processing contract.");
            return;
        }

        if (message is null)
        {
            await args.DeadLetterMessageAsync(args.Message, "EmptyDocumentMessage", "The message body was empty.");
            return;
        }

        logger.LogInformation("Processing document {DocumentId}; message {MessageId}; delivery {DeliveryCount}.",
            message.DocumentId, args.Message.MessageId, args.Message.DeliveryCount);

        try
        {
            var status = await GetStatusAsync(message.DocumentId, args.CancellationToken);
            if (status is null)
            {
                await args.DeadLetterMessageAsync(args.Message, "DocumentNotFound", $"Document {message.DocumentId} no longer exists.");
                return;
            }

            // This is the idempotency guard: Service Bus is at-least-once, so a duplicate must be safe.
            if (status == Completed)
            {
                logger.LogInformation("Document {DocumentId} was already complete; completing duplicate message.", message.DocumentId);
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                return;
            }

            await SetStatusAsync(message.DocumentId, "Processing", null, null, args.CancellationToken);
            await PerformDocumentProcessingAsync(message, args.CancellationToken);
            await SetStatusAsync(message.DocumentId, Completed, null, DateTimeOffset.UtcNow, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            logger.LogInformation("Completed document {DocumentId}.", message.DocumentId);
        }
        catch (Exception exception) when (!args.CancellationToken.IsCancellationRequested)
        {
            await SetStatusAsync(message.DocumentId, "Failed", exception.Message, null, CancellationToken.None);
            logger.LogError(exception, "Document {DocumentId} failed. Service Bus will retry or dead-letter it.", message.DocumentId);
            throw; // Do not complete: Service Bus applies its configured retry/dead-letter policy.
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus error from {ErrorSource} on {EntityPath}.", args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }

    private static async Task PerformDocumentProcessingAsync(DocumentProcessingMessage message, CancellationToken cancellationToken)
    {
        // Replace with virus scanning/OCR/PDF extraction. Keeping it explicit makes a demo safe and repeatable.
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    }

    private async Task<string?> GetStatusAsync(Guid documentId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT [ProcessingStatus] FROM [dbo].[Documents] WHERE [Id] = @id", connection);
        command.Parameters.AddWithValue("@id", documentId);
        return (await command.ExecuteScalarAsync(cancellationToken)) as string;
    }

    private async Task SetStatusAsync(Guid documentId, string status, string? error, DateTimeOffset? completedAtUtc, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("""
            UPDATE [dbo].[Documents]
            SET [ProcessingStatus] = @status, [LastError] = @error, [ProcessedAtUtc] = @processedAtUtc
            WHERE [Id] = @id
            """, connection);
        command.Parameters.AddWithValue("@id", documentId);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@error", (object?)error ?? DBNull.Value);
        command.Parameters.AddWithValue("@processedAtUtc", (object?)completedAtUtc ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("AzureSql")
            ?? throw new InvalidOperationException("ConnectionStrings:AzureSql is required for the worker.");
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
