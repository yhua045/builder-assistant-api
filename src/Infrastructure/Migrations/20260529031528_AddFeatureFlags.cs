using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BuilderAssistantApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Features",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DefaultEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Features", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleEntitlements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleEntitlements", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Features",
                columns: new[] { "Id", "DefaultEnabled", "Description", "Key" },
                values: new object[,]
                {
                    { 1L, false, "OCR scan for invoices and receipts", "ocr_scan" },
                    { 2L, false, "High-rate API access", "high_rate_api" }
                });

            // Seed: Admin role gets all features; Premium gets ocr_scan
            migrationBuilder.InsertData(
                table: "RoleEntitlements",
                columns: new[] { "RoleName", "FeatureKey", "Enabled", "ExpiresAt", "CreatedAt" },
                values: new object[,]
                {
                    { "Admin",   "ocr_scan",      true, null, new DateTimeOffset(2026, 5, 29, 3, 15, 28, TimeSpan.Zero) },
                    { "Admin",   "high_rate_api", true, null, new DateTimeOffset(2026, 5, 29, 3, 15, 28, TimeSpan.Zero) },
                    { "Premium", "ocr_scan",      true, null, new DateTimeOffset(2026, 5, 29, 3, 15, 28, TimeSpan.Zero) }
                });

            migrationBuilder.CreateIndex(
                name: "UX_Features_Key",
                table: "Features",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleEntitlements_RoleName_FeatureKey",
                table: "RoleEntitlements",
                columns: new[] { "RoleName", "FeatureKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Features");

            migrationBuilder.DropTable(
                name: "RoleEntitlements");
        }
    }
}

