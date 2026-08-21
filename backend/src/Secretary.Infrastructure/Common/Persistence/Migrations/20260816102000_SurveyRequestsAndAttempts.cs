using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secretary.Infrastructure.Common.Persistence.Migrations
{
    /// <summary>Splits "a survey call" into the person and the dials against them.
    ///
    /// ⚠ Hand-edited after scaffolding, for the second time in this module and the same reason.
    /// EF saw SurveyId leaving Calls and SurveyRequestId arriving and offered a rename — which
    /// would have pointed every existing call at a request whose id happened to equal its old
    /// questionnaire id. It also dropped PersonName and PhoneNumber before anything had read
    /// them, and defaulted RetryDelayMinutes to 0, which the entity itself refuses.
    ///
    /// So: the table and the columns first, then a request built out of every existing call, then
    /// the old columns go.</summary>
    public partial class SurveyRequestsAndAttempts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                schema: "fd",
                table: "Surveys",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // 60, not 0. Zero is not a delay the entity will accept, and a default the domain
            // rejects is a row nobody can save again after loading it.
            migrationBuilder.AddColumn<int>(
                name: "RetryDelayMinutes",
                schema: "fd",
                table: "Surveys",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.CreateTable(
                name: "Requests",
                schema: "fd",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    SurveyId = table.Column<int>(type: "int", nullable: false),
                    PersonName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextAttemptDueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Requests_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalSchema: "fd",
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Requests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_NextAttemptDueAt",
                schema: "fd",
                table: "Requests",
                column: "NextAttemptDueAt",
                filter: "[NextAttemptDueAt] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_SurveyId",
                schema: "fd",
                table: "Requests",
                column: "SurveyId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_TenantId_CreatedAtUtc",
                schema: "fd",
                table: "Requests",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            // New column, nullable while it is filled in, then made required.
            migrationBuilder.AddColumn<int>(
                name: "SurveyRequestId",
                schema: "fd",
                table: "Calls",
                type: "int",
                nullable: true);

            BackfillRequests(migrationBuilder);

            migrationBuilder.AlterColumn<int>(
                name: "SurveyRequestId",
                schema: "fd",
                table: "Calls",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropForeignKey(name: "FK_Calls_Surveys_SurveyId", schema: "fd", table: "Calls");
            migrationBuilder.DropIndex(name: "IX_Calls_SurveyId", schema: "fd", table: "Calls");
            migrationBuilder.DropColumn(name: "SurveyId", schema: "fd", table: "Calls");
            migrationBuilder.DropColumn(name: "PersonName", schema: "fd", table: "Calls");
            migrationBuilder.DropColumn(name: "PhoneNumber", schema: "fd", table: "Calls");

            migrationBuilder.CreateIndex(
                name: "IX_Calls_SurveyRequestId",
                schema: "fd",
                table: "Calls",
                column: "SurveyRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Calls_Requests_SurveyRequestId",
                schema: "fd",
                table: "Calls",
                column: "SurveyRequestId",
                principalSchema: "fd",
                principalTable: "Requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <summary>One request per existing call, and deliberately not one per person.
        ///
        /// Merging by name and number would have made the 15 August rows read as one person rung
        /// five times, which is what actually happened and is the nicer answer. It is not the safe
        /// answer: nothing in this schema says two rows with the same number are the same person's
        /// same survey, and a migration that decides they are will one day merge two people who
        /// share a phone. History stays as the system recorded it; the shape is right from here on.</summary>
        private static void BackfillRequests(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql(
                """
                DECLARE @mapped TABLE (CallId int, RequestId int);

                MERGE fd.Requests AS target
                USING (
                    SELECT c.Id AS CallId, c.TenantId, c.SurveyId, c.PersonName, c.PhoneNumber,
                           c.CreatedAtUtc, c.StartedAt, c.CallStatus, c.CallerTurnCount,
                           c.CreatedAt, c.UpdatedAt, c.Status
                    FROM   fd.Calls c
                ) AS source
                ON 1 = 0
                WHEN NOT MATCHED THEN
                    INSERT (TenantId, SurveyId, PersonName, PhoneNumber, Outcome, AttemptCount,
                            LastAttemptAt, NextAttemptDueAt, CreatedAtUtc, CreatedAt, UpdatedAt, Status)
                    VALUES (source.TenantId, source.SurveyId, source.PersonName, source.PhoneNumber,
                            -- The same reading the entity does: completed is complete, handed over
                            -- is handed over, and anything with a caller turn on it was a
                            -- conversation that did not finish. The rest reached nobody.
                            CASE source.CallStatus
                                WHEN 2 THEN 4
                                WHEN 4 THEN 3
                                ELSE CASE WHEN source.CallerTurnCount > 0 THEN 2 ELSE 1 END
                            END,
                            1,
                            source.StartedAt,
                            NULL,
                            source.CreatedAtUtc, source.CreatedAt, source.UpdatedAt, source.Status)
                    OUTPUT source.CallId, inserted.Id INTO @mapped (CallId, RequestId);

                UPDATE c
                SET    c.SurveyRequestId = m.RequestId
                FROM   fd.Calls c
                       JOIN @mapped m ON m.CallId = c.Id;
                """);

        /// <summary>⚠ Lossy, and says so. Going back collapses every attempt onto its request's
        /// questionnaire and loses the retry state entirely — a request rung three times becomes
        /// three calls again with no record that they were the same person.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SurveyId", schema: "fd", table: "Calls", type: "int", nullable: false, defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PersonName", schema: "fd", table: "Calls",
                type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber", schema: "fd", table: "Calls",
                type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE c
                SET    c.SurveyId = r.SurveyId,
                       c.PersonName = r.PersonName,
                       c.PhoneNumber = r.PhoneNumber
                FROM   fd.Calls c
                       JOIN fd.Requests r ON r.Id = c.SurveyRequestId;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Calls_Requests_SurveyRequestId", schema: "fd", table: "Calls");
            migrationBuilder.DropIndex(name: "IX_Calls_SurveyRequestId", schema: "fd", table: "Calls");
            migrationBuilder.DropColumn(name: "SurveyRequestId", schema: "fd", table: "Calls");
            migrationBuilder.DropTable(name: "Requests", schema: "fd");
            migrationBuilder.DropColumn(name: "RetryCount", schema: "fd", table: "Surveys");
            migrationBuilder.DropColumn(name: "RetryDelayMinutes", schema: "fd", table: "Surveys");

            migrationBuilder.CreateIndex(
                name: "IX_Calls_SurveyId", schema: "fd", table: "Calls", column: "SurveyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Calls_Surveys_SurveyId", schema: "fd", table: "Calls", column: "SurveyId",
                principalSchema: "fd", principalTable: "Surveys", principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
