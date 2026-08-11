using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secretary.Infrastructure.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessHoursAndOrderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                schema: "ord",
                table: "Products",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "BusinessHours",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    OpensAt = table.Column<int>(type: "int", nullable: true),
                    ClosesAt = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessHours_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderSettings",
                schema: "ord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    LeadWorkingDays = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessHours_TenantId_DayOfWeek",
                schema: "dbo",
                table: "BusinessHours",
                columns: new[] { "TenantId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderSettings_TenantId",
                schema: "ord",
                table: "OrderSettings",
                column: "TenantId",
                unique: true);

            // Every existing tenant gets the hours they already had.
            //
            // This is not a convenience. A day with no row is closed — deliberately, so a tenant
            // who has never set their hours offers nothing rather than everything — which means
            // that without this backfill the constant this migration replaces would become
            // "closed all week" for every tenant already using the system, and the agent would
            // answer "when are you free?" with nothing at all.
            //
            // 540 and 1260 are 09:00 and 21:00 as minutes since midnight, matching the
            // BusinessOpenHour/BusinessCloseHour constants removed from AppointmentTools. Days
            // are IsoDayOfWeek: Monday 1 through Sunday 7. Status 0 is EntityStatus.Active.
            migrationBuilder.Sql(
                """
                INSERT INTO [dbo].[BusinessHours]
                    ([TenantId], [DayOfWeek], [OpensAt], [ClosesAt], [CreatedAt], [UpdatedAt], [Status])
                SELECT t.[Id], d.[DayOfWeek], 540, 1260, SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [dbo].[Tenants] t
                CROSS JOIN (VALUES (1), (2), (3), (4), (5), (6), (7)) AS d([DayOfWeek])
                WHERE NOT EXISTS (
                    SELECT 1 FROM [dbo].[BusinessHours] h
                    WHERE h.[TenantId] = t.[Id] AND h.[DayOfWeek] = d.[DayOfWeek]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessHours",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OrderSettings",
                schema: "ord");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                schema: "ord",
                table: "Products");
        }
    }
}
