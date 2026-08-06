using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secretary.Infrastructure.Common.Persistence.Migrations
{
    /// <summary>Gives the Appointment module its own schema. Every operation here is a move,
    /// not a rebuild — on SQL Server RenameTable-with-newSchema emits ALTER SCHEMA ... TRANSFER,
    /// which carries the rows, indexes and foreign keys across untouched. Nothing is dropped and
    /// nothing is copied, so this is safe to run against a populated database.</summary>
    public partial class MoveAppointmentTablesToAppSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.RenameTable(
                name: "ServiceOfferings",
                newName: "ServiceOfferings",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "ProviderServiceOfferings",
                newName: "ProviderServiceOfferings",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "Providers",
                newName: "Providers",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "Escalations",
                newName: "Escalations",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "Clients",
                newName: "Clients",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "Calls",
                newName: "Calls",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "Appointments",
                newName: "Appointments",
                newSchema: "app");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "ServiceOfferings",
                schema: "app",
                newName: "ServiceOfferings");

            migrationBuilder.RenameTable(
                name: "ProviderServiceOfferings",
                schema: "app",
                newName: "ProviderServiceOfferings");

            migrationBuilder.RenameTable(
                name: "Providers",
                schema: "app",
                newName: "Providers");

            migrationBuilder.RenameTable(
                name: "Escalations",
                schema: "app",
                newName: "Escalations");

            migrationBuilder.RenameTable(
                name: "Clients",
                schema: "app",
                newName: "Clients");

            migrationBuilder.RenameTable(
                name: "Calls",
                schema: "app",
                newName: "Calls");

            migrationBuilder.RenameTable(
                name: "Appointments",
                schema: "app",
                newName: "Appointments");
        }
    }
}
