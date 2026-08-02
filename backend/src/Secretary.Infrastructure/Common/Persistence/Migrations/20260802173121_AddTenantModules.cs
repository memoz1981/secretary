using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secretary.Infrastructure.Common.Persistence.Migrations
{
    /// <summary>Adds the TenantModules table and grants Appointment to every tenant that already
    /// exists.
    ///
    /// Hand-trimmed. EF generated this as a create-everything migration because the SQL Server
    /// line had no model snapshot: the shared AppDbContextModelSnapshot.cs was moved into
    /// Migrations.Postgres when the PostgreSQL work was parked, and that folder is excluded from
    /// compilation, so EF was diffing against an empty model. The snapshot has been restored
    /// alongside this file from its own Designer output, and this migration reduced to what it
    /// actually does.</summary>
    public partial class AddTenantModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Module = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantModules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantModules_TenantId_Module",
                table: "TenantModules",
                columns: new[] { "TenantId", "Module" },
                unique: true);

            // Every tenant that existed before modules did was, in effect, an Appointment tenant
            // — it is the only module built. Without this they would all wake up holding nothing
            // and be shut out of the app the moment the guard goes live.
            //
            // Module 1 = Appointment, Status 0 = EntityStatus.Active. Written as literals because
            // a migration has to keep meaning what it meant on the day it ran, even if those
            // enums are renumbered afterwards.
            migrationBuilder.Sql("""
                INSERT INTO [TenantModules] ([TenantId], [Module], [CreatedAt], [UpdatedAt], [Status])
                SELECT t.[Id], 1, SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [Tenants] t
                WHERE NOT EXISTS (
                    SELECT 1 FROM [TenantModules] tm
                    WHERE tm.[TenantId] = t.[Id] AND tm.[Module] = 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TenantModules");
        }
    }
}
