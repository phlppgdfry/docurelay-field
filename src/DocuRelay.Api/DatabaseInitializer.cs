using Microsoft.EntityFrameworkCore;

namespace DocuRelay.Api;

public sealed class DatabaseInitializer(IServiceScopeFactory scopeFactory, ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DocuRelayDbContext>();
        if (database.Database.IsRelational())
        {
            await database.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await database.Database.EnsureCreatedAsync(cancellationToken);
        }

        if (await database.Dossiers.AnyAsync(cancellationToken))
        {
            return;
        }

        database.Dossiers.AddRange(
            new DossierEntity
            {
                Id = Guid.Parse("cb0a7aad-05ff-42d7-b6dd-182b98ff11da"),
                Reference = "DR-2026-0042",
                Title = "Cooling unit maintenance",
                Location = "Brugge",
                UpdatedAtUtc = DateTimeOffset.UtcNow.AddHours(-2)
            },
            new DossierEntity
            {
                Id = Guid.Parse("5f119a79-69e5-49bf-a77a-b3d37e72579f"),
                Reference = "DR-2026-0041",
                Title = "Safety documentation review",
                Location = "Zeebrugge",
                UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
            });
        await database.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded the DocuRelay Azure SQL database.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
