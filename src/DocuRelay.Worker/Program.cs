using Azure.Identity;
using Azure.Messaging.ServiceBus;
using DocuRelay.Worker;

var builder = Host.CreateApplicationBuilder(args);
var serviceBusNamespace = builder.Configuration["ServiceBus:FullyQualifiedNamespace"];

if (!string.IsNullOrWhiteSpace(serviceBusNamespace))
{
    builder.Services.AddSingleton(new ServiceBusClient(serviceBusNamespace, new DefaultAzureCredential()));
}

builder.Services.AddHostedService<DocumentProcessorWorker>();
await builder.Build().RunAsync();
