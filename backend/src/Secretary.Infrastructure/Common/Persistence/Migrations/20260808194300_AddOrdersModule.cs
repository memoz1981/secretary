using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secretary.Infrastructure.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdersModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ord");

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "ord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Customers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                schema: "ord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAddresses",
                schema: "ord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    District = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Lane = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LaneNumber = table.Column<int>(type: "int", nullable: true),
                    Building = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Apartment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Landmark = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SpokenText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    StreetNormalized = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BuildingNormalized = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "ord",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPhoneNumbers",
                schema: "ord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPhoneNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPhoneNumbers_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "ord",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "ord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MeasurementUnitId = table.Column<int>(type: "int", nullable: false),
                    Aliases = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Units_MeasurementUnitId",
                        column: x => x.MeasurementUnitId,
                        principalSchema: "ord",
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "ord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CustomerAddressId = table.Column<int>(type: "int", nullable: false),
                    OrderStatus = table.Column<int>(type: "int", nullable: false),
                    PlacedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_CustomerAddresses_CustomerAddressId",
                        column: x => x.CustomerAddressId,
                        principalSchema: "ord",
                        principalTable: "CustomerAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "ord",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                schema: "ord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "ord",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "ord",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_CustomerId",
                schema: "ord",
                table: "CustomerAddresses",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_District_StreetNormalized_LaneNumber_BuildingNormalized",
                schema: "ord",
                table: "CustomerAddresses",
                columns: new[] { "District", "StreetNormalized", "LaneNumber", "BuildingNormalized" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPhoneNumbers_CustomerId_PhoneNumber",
                schema: "ord",
                table: "CustomerPhoneNumbers",
                columns: new[] { "CustomerId", "PhoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPhoneNumbers_PhoneNumber",
                schema: "ord",
                table: "CustomerPhoneNumbers",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId",
                schema: "ord",
                table: "Customers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId",
                schema: "ord",
                table: "OrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_ProductId",
                schema: "ord",
                table: "OrderLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerAddressId",
                schema: "ord",
                table: "Orders",
                column: "CustomerAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId",
                schema: "ord",
                table: "Orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_PlacedAt",
                schema: "ord",
                table: "Orders",
                columns: new[] { "TenantId", "PlacedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_MeasurementUnitId",
                schema: "ord",
                table: "Products",
                column: "MeasurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId",
                schema: "ord",
                table: "Products",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_Name",
                schema: "ord",
                table: "Units",
                column: "Name",
                unique: true);

            // The three units the business actually sells in, metric. Seeded here rather than by
            // the dev seeder because production needs them too and a product cannot exist
            // without one — an empty Units table means an empty catalogue.
            //
            // Guarded on the name so re-running against a database that already has them is a
            // no-op rather than a unique-index violation. Status 0 is EntityStatus.Active.
            migrationBuilder.Sql(
                """
                INSERT INTO [ord].[Units] ([Name], [CreatedAt], [UpdatedAt], [Status])
                SELECT seed.[Name], SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM (VALUES ('ea.'), ('kg'), ('m3')) AS seed([Name])
                WHERE NOT EXISTS (SELECT 1 FROM [ord].[Units] u WHERE u.[Name] = seed.[Name]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerPhoneNumbers",
                schema: "ord");

            migrationBuilder.DropTable(
                name: "OrderLines",
                schema: "ord");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "ord");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "ord");

            migrationBuilder.DropTable(
                name: "CustomerAddresses",
                schema: "ord");

            migrationBuilder.DropTable(
                name: "Units",
                schema: "ord");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "ord");
        }
    }
}
