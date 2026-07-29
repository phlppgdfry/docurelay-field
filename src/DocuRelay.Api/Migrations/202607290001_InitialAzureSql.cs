using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocuRelay.Api.Migrations;

[DbContext(typeof(DocuRelayDbContext))]
[Migration("202607290001_InitialAzureSql")]
public partial class InitialAzureSql : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Dossiers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Reference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Location = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Dossiers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Documents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DossierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(127)", maxLength: 127, nullable: false),
                StoragePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ProcessingStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                LastError = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Documents", x => x.Id);
                table.ForeignKey("FK_Documents_Dossiers_DossierId", x => x.DossierId, "Dossiers", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DispatchedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                LastError = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_OutboxMessages", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_Documents_DossierId", table: "Documents", column: "DossierId");
        migrationBuilder.CreateIndex(name: "IX_Documents_ProcessingStatus", table: "Documents", column: "ProcessingStatus");
        migrationBuilder.CreateIndex(name: "IX_Dossiers_Reference", table: "Dossiers", column: "Reference", unique: true);
        migrationBuilder.CreateIndex(name: "IX_OutboxMessages_DispatchedAtUtc", table: "OutboxMessages", column: "DispatchedAtUtc");
        migrationBuilder.CreateIndex(name: "IX_OutboxMessages_DocumentId", table: "OutboxMessages", column: "DocumentId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OutboxMessages");
        migrationBuilder.DropTable(name: "Documents");
        migrationBuilder.DropTable(name: "Dossiers");
    }
}
