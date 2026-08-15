using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secretary.Infrastructure.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fd");

            migrationBuilder.CreateTable(
                name: "FeedbackSettings",
                schema: "fd",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    MaxSurveys = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Surveys",
                schema: "fd",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Surveys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Surveys_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Calls",
                schema: "fd",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    SurveyId = table.Column<int>(type: "int", nullable: false),
                    PersonName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CallStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    TurnCount = table.Column<int>(type: "int", nullable: false),
                    CallerTurnCount = table.Column<int>(type: "int", nullable: false),
                    Transcript = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                        name: "FK_Calls_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalSchema: "fd",
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Calls_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SurveyQuestions",
                schema: "fd",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    QuestionType = table.Column<int>(type: "int", nullable: false),
                    IsHeadline = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyQuestions_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalSchema: "fd",
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SurveyQuestionOptions",
                schema: "fd",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyQuestionId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyQuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyQuestionOptions_SurveyQuestions_SurveyQuestionId",
                        column: x => x.SurveyQuestionId,
                        principalSchema: "fd",
                        principalTable: "SurveyQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Answers",
                schema: "fd",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeedbackCallId = table.Column<int>(type: "int", nullable: false),
                    SurveyQuestionId = table.Column<int>(type: "int", nullable: false),
                    SurveyQuestionOptionId = table.Column<int>(type: "int", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Declined = table.Column<bool>(type: "bit", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Answers_Calls_FeedbackCallId",
                        column: x => x.FeedbackCallId,
                        principalSchema: "fd",
                        principalTable: "Calls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Answers_SurveyQuestionOptions_SurveyQuestionOptionId",
                        column: x => x.SurveyQuestionOptionId,
                        principalSchema: "fd",
                        principalTable: "SurveyQuestionOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Answers_SurveyQuestions_SurveyQuestionId",
                        column: x => x.SurveyQuestionId,
                        principalSchema: "fd",
                        principalTable: "SurveyQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Answers_FeedbackCallId_SurveyQuestionId",
                schema: "fd",
                table: "Answers",
                columns: new[] { "FeedbackCallId", "SurveyQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Answers_SurveyQuestionId",
                schema: "fd",
                table: "Answers",
                column: "SurveyQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Answers_SurveyQuestionOptionId",
                schema: "fd",
                table: "Answers",
                column: "SurveyQuestionOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Calls_SurveyId",
                schema: "fd",
                table: "Calls",
                column: "SurveyId");

            migrationBuilder.CreateIndex(
                name: "IX_Calls_TenantId_CreatedAtUtc",
                schema: "fd",
                table: "Calls",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackSettings_TenantId",
                schema: "fd",
                table: "FeedbackSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurveyQuestionOptions_SurveyQuestionId_Position",
                schema: "fd",
                table: "SurveyQuestionOptions",
                columns: new[] { "SurveyQuestionId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyQuestions_SurveyId_Position",
                schema: "fd",
                table: "SurveyQuestions",
                columns: new[] { "SurveyId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_Surveys_TenantId",
                schema: "fd",
                table: "Surveys",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Calls_Tenants_TenantId",
                schema: "app",
                table: "Calls");

            migrationBuilder.DropTable(
                name: "Answers",
                schema: "fd");

            migrationBuilder.DropTable(
                name: "FeedbackSettings",
                schema: "fd");

            migrationBuilder.DropTable(
                name: "Calls",
                schema: "fd");

            migrationBuilder.DropTable(
                name: "SurveyQuestionOptions",
                schema: "fd");

            migrationBuilder.DropTable(
                name: "SurveyQuestions",
                schema: "fd");

            migrationBuilder.DropTable(
                name: "Surveys",
                schema: "fd");
        }
    }
}
