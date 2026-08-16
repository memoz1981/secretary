using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secretary.Infrastructure.Common.Persistence.Migrations
{
    /// <summary>Four question types, and a derived percentage instead of a typed number.
    ///
    /// ⚠ Hand-edited after scaffolding, twice over. EF saw IsHeadline and YesIsPositive as two
    /// bit columns and offered a rename, which would have carried "this is the headline" into
    /// "yes is the good answer" — true of one existing question by luck and wrong in principle.
    /// And it dropped Value without reading it, which would have thrown away every score on the
    /// way to a schema whose whole point is scores.
    ///
    /// So the columns are added first, the existing questionnaires are converted from what is in
    /// them, and only then do the old columns go.</summary>
    public partial class FeedbackQuestionShapes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowOther",
                schema: "fd",
                table: "SurveyQuestions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CountsTowardScore",
                schema: "fd",
                table: "SurveyQuestions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "YesIsPositive",
                schema: "fd",
                table: "SurveyQuestions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ScaleMax",
                schema: "fd",
                table: "SurveyQuestions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOther",
                schema: "fd",
                table: "SurveyQuestionOptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ScorePercent",
                schema: "fd",
                table: "SurveyQuestionOptions",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            ConvertExistingQuestions(migrationBuilder);

            migrationBuilder.DropColumn(name: "Value", schema: "fd", table: "SurveyQuestionOptions");
            migrationBuilder.DropColumn(name: "IsHeadline", schema: "fd", table: "SurveyQuestions");
        }

        /// <summary>Reads the shape out of the options rather than asking anybody.
        ///
        /// Every existing question is a Choice, because Choice and Open were the only two types.
        /// A question whose options are exactly 1..N is the scale somebody built by hand; two
        /// options reading Bəli and Xeyr is a yes/no; anything else was always a real choice and
        /// stays one. Nothing is guessed beyond that — a partly-numeric list keeps its counts and
        /// loses only a score that never meant anything.</summary>
        private static void ConvertExistingQuestions(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql(
                """
                SELECT q.Id,
                       COUNT(o.Id)                                                  AS OptionCount,
                       SUM(CASE WHEN o.Text NOT LIKE '%[^0-9]%' AND LEN(o.Text) > 0
                                THEN 1 ELSE 0 END)                                  AS NumericCount,
                       SUM(CASE WHEN o.Text IN (N'Bəli', N'Beli', N'Xeyr')
                                THEN 1 ELSE 0 END)                                  AS YesNoCount,
                       MIN(CASE WHEN o.Text NOT LIKE '%[^0-9]%' AND LEN(o.Text) > 0
                                THEN CAST(o.Text AS int) END)                       AS Lowest,
                       MAX(CASE WHEN o.Text NOT LIKE '%[^0-9]%' AND LEN(o.Text) > 0
                                THEN CAST(o.Text AS int) END)                       AS Highest
                INTO   #shape
                FROM   fd.SurveyQuestions q
                       JOIN fd.SurveyQuestionOptions o ON o.SurveyQuestionId = q.Id
                WHERE  q.QuestionType = 1
                GROUP BY q.Id;

                -- A scale, and only if the options really are 1..N for an N we allow. "2, 4, 6"
                -- is three numeric options and is not a three-point scale; treating it as one
                -- would score the 6 at 150%.
                UPDATE q
                SET    q.QuestionType = 3,
                       q.ScaleMax = s.OptionCount
                FROM   fd.SurveyQuestions q
                       JOIN #shape s ON s.Id = q.Id
                WHERE  s.NumericCount = s.OptionCount
                  AND  s.Lowest = 1
                  AND  s.Highest = s.OptionCount
                  AND  s.OptionCount IN (3, 5, 10);

                UPDATE q
                SET    q.QuestionType = 2,
                       q.YesIsPositive = 1
                FROM   fd.SurveyQuestions q
                       JOIN #shape s ON s.Id = q.Id
                WHERE  s.OptionCount = 2
                  AND  s.YesNoCount = 2;

                -- (i-1)/(N-1), the same arithmetic the entity does.
                UPDATE o
                SET    o.ScorePercent = ROUND((CAST(o.Text AS decimal(9, 4)) - 1) * 100.0 / (q.ScaleMax - 1), 2)
                FROM   fd.SurveyQuestionOptions o
                       JOIN fd.SurveyQuestions q ON q.Id = o.SurveyQuestionId
                WHERE  q.QuestionType = 3;

                -- The labels become the ones the entity owns, so an ASCII "Beli" typed in a form
                -- stops being a second spelling the matcher has to know about.
                UPDATE o
                SET    o.Text = N'Bəli', o.ScorePercent = 100
                FROM   fd.SurveyQuestionOptions o
                       JOIN fd.SurveyQuestions q ON q.Id = o.SurveyQuestionId
                WHERE  q.QuestionType = 2 AND o.Text IN (N'Bəli', N'Beli');

                UPDATE o
                SET    o.Text = N'Xeyr', o.ScorePercent = 0
                FROM   fd.SurveyQuestionOptions o
                       JOIN fd.SurveyQuestions q ON q.Id = o.SurveyQuestionId
                WHERE  q.QuestionType = 2 AND o.Text = N'Xeyr';

                -- The headline was the one question whose number led the page, which is the
                -- closest thing the old schema had to "this counts". Only where the new type has
                -- a score to contribute.
                UPDATE fd.SurveyQuestions
                SET    CountsTowardScore = 1
                WHERE  IsHeadline = 1 AND QuestionType IN (2, 3);

                DROP TABLE #shape;
                """);

        /// <summary>⚠ Lossy, and says so. Going back turns every percentage into whatever integer
        /// it happened to be, and there is no way to recover which question was the headline —
        /// several may now count toward the score. The forward path is the supported one.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Value",
                schema: "fd",
                table: "SurveyQuestionOptions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHeadline",
                schema: "fd",
                table: "SurveyQuestions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE fd.SurveyQuestionOptions
                SET    Value = CAST(ScorePercent AS int)
                WHERE  ScorePercent IS NOT NULL;

                UPDATE fd.SurveyQuestions SET QuestionType = 1 WHERE QuestionType IN (2, 3);
                """);

            migrationBuilder.DropColumn(name: "AllowOther", schema: "fd", table: "SurveyQuestions");
            migrationBuilder.DropColumn(name: "CountsTowardScore", schema: "fd", table: "SurveyQuestions");
            migrationBuilder.DropColumn(name: "ScaleMax", schema: "fd", table: "SurveyQuestions");
            migrationBuilder.DropColumn(name: "YesIsPositive", schema: "fd", table: "SurveyQuestions");
            migrationBuilder.DropColumn(name: "IsOther", schema: "fd", table: "SurveyQuestionOptions");
            migrationBuilder.DropColumn(name: "ScorePercent", schema: "fd", table: "SurveyQuestionOptions");
        }
    }
}
