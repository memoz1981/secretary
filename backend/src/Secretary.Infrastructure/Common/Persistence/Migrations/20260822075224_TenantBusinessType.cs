using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secretary.Infrastructure.Common.Persistence.Migrations
{
    /// <summary>What kind of business each tenant runs.
    ///
    /// Nullable with no default, deliberately. Every other option writes a guess into the one
    /// column whose purpose is telling the customer base apart: defaulting to Other claims an
    /// admin looked at the list and rejected it, and defaulting to Clinic is simply wrong. NULL
    /// says nobody has been asked yet, which is the truth about every row that exists today.</summary>
    public partial class TenantBusinessType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BusinessType",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_BusinessType",
                table: "Tenants",
                column: "BusinessType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_BusinessType",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "Tenants");
        }
    }
}
