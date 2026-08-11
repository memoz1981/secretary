using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secretary.Infrastructure.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Calls",
                schema: "ord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    CallerPhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RelatedOrderId = table.Column<int>(type: "int", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    TurnCount = table.Column<int>(type: "int", nullable: false),
                    CallerTurnCount = table.Column<int>(type: "int", nullable: false),
                    WaitTimeSeconds = table.Column<int>(type: "int", nullable: true),
                    RecordingUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Transcript = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AgentModel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Pipeline = table.Column<int>(type: "int", nullable: false),
                    InputTextTokens = table.Column<int>(type: "int", nullable: false),
                    CachedInputTextTokens = table.Column<int>(type: "int", nullable: false),
                    InputAudioTokens = table.Column<int>(type: "int", nullable: false),
                    CachedInputAudioTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTextTokens = table.Column<int>(type: "int", nullable: false),
                    OutputAudioTokens = table.Column<int>(type: "int", nullable: false),
                    CostUsd = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Calls_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "ord",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Calls_Orders_RelatedOrderId",
                        column: x => x.RelatedOrderId,
                        principalSchema: "ord",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Calls_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Calls_CustomerId",
                schema: "ord",
                table: "Calls",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Calls_RelatedOrderId",
                schema: "ord",
                table: "Calls",
                column: "RelatedOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Calls_TenantId_StartedAt",
                schema: "ord",
                table: "Calls",
                columns: new[] { "TenantId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Calls_Tenants_TenantId",
                schema: "app",
                table: "Calls");

            migrationBuilder.DropTable(
                name: "Calls",
                schema: "ord");
        }
    }
}
