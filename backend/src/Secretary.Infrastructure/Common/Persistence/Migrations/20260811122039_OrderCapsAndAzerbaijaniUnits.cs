using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secretary.Infrastructure.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderCapsAndAzerbaijaniUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                schema: "ord",
                table: "Products");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxOrderQuantity",
                schema: "ord",
                table: "Products",
                type: "decimal(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxDeliveryDaysAhead",
                schema: "ord",
                table: "OrderSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // A window of zero days ends today, which would refuse every delivery day including
            // the one the agent offers first. Existing rows get the default the entity uses.
            migrationBuilder.Sql(
                "UPDATE [ord].[OrderSettings] SET [MaxDeliveryDaysAhead] = 30 WHERE [MaxDeliveryDaysAhead] < 1;");

            // "ea." is the only unit that is not already the same word in Azerbaijani; kg and m3
            // are read as written. Renamed rather than translated at the edges, because this
            // string is both what the tenant picks in the Products form and what the agent says
            // out loud — two spellings of one unit is how a catalogue splits in half.
            migrationBuilder.Sql(
                """
                UPDATE [ord].[Units] SET [Name] = N'ədəd', [UpdatedAt] = SYSUTCDATETIME()
                WHERE [Name] = 'ea.' AND NOT EXISTS (SELECT 1 FROM [ord].[Units] u WHERE u.[Name] = N'ədəd');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxOrderQuantity",
                schema: "ord",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MaxDeliveryDaysAhead",
                schema: "ord",
                table: "OrderSettings");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "ord",
                table: "Products",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
