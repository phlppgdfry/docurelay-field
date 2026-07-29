using Microsoft.EntityFrameworkCore;

namespace DocuRelay.Api;

public sealed class DocuRelayDbContext(DbContextOptions<DocuRelayDbContext> options) : DbContext(options)
{
    public DbSet<DossierEntity> Dossiers => Set<DossierEntity>();
    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DossierEntity>(entity =>
        {
            entity.ToTable("Dossiers");
            entity.HasKey(dossier => dossier.Id);
            entity.Property(dossier => dossier.Reference).HasMaxLength(32).IsRequired();
            entity.HasIndex(dossier => dossier.Reference).IsUnique();
            entity.Property(dossier => dossier.Title).HasMaxLength(256).IsRequired();
            entity.Property(dossier => dossier.Location).HasMaxLength(128).IsRequired();
            entity.HasMany(dossier => dossier.Documents).WithOne(document => document.Dossier).HasForeignKey(document => document.DossierId);
        });

        modelBuilder.Entity<DocumentEntity>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(document => document.Id);
            entity.Property(document => document.FileName).HasMaxLength(260).IsRequired();
            entity.Property(document => document.ContentType).HasMaxLength(127).IsRequired();
            entity.Property(document => document.StoragePath).HasMaxLength(512).IsRequired();
            entity.Property(document => document.ProcessingStatus).HasMaxLength(32).IsRequired();
            entity.Property(document => document.LastError).HasMaxLength(2048);
            entity.HasIndex(document => document.ProcessingStatus);
        });

        modelBuilder.Entity<OutboxMessageEntity>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Payload).IsRequired();
            entity.Property(message => message.LastError).HasMaxLength(2048);
            entity.HasIndex(message => message.DispatchedAtUtc);
            entity.HasIndex(message => message.DocumentId);
        });
    }
}

public sealed class DossierEntity
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public List<DocumentEntity> Documents { get; set; } = [];
}

public sealed class DocumentEntity
{
    public Guid Id { get; set; }
    public Guid DossierId { get; set; }
    public DossierEntity Dossier { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; }
    public string ProcessingStatus { get; set; } = "Pending";
    public string? LastError { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
}

public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DispatchedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}
