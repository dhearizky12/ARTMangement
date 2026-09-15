using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BantuBantu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProfileWizardAndServiceCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfileStep",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UploadedAt",
                table: "UserDocuments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "UserAddresses",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Province",
                table: "UserAddresses",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ServiceCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IconKey = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCategories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ServiceCategories",
                columns: new[] { "Id", "Description", "IconKey", "IsActive", "IsFeatured", "Name", "Slug", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("ceaba520-c877-44b0-8950-a590c3277101"), "Bantuan untuk rutinitas rumah, dari merapikan ruangan hingga kebutuhan harian keluarga.", "house", true, true, "Asisten rumah tangga", "asisten-rumah-tangga", 1 },
                    { new Guid("ceaba520-c877-44b0-8950-a590c3277102"), "Kenali layanan pengemudi untuk perjalanan harian dan mobilitas keluarga Anda.", "car", true, true, "Driver", "driver", 2 },
                    { new Guid("ceaba520-c877-44b0-8950-a590c3277103"), "Bantuan membersihkan rumah dan ruang kerja supaya hari terasa lebih nyaman.", "sparkles", true, false, "Kebersihan", "kebersihan", 3 },
                    { new Guid("ceaba520-c877-44b0-8950-a590c3277104"), "Temukan jenis bantuan untuk merawat tanaman dan menjaga halaman tetap rapi.", "sprout", true, false, "Perawatan taman", "perawatan-taman", 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCategories_Slug",
                table: "ServiceCategories",
                column: "Slug",
                unique: true);
            migrationBuilder.Sql("""
                UPDATE "Users" AS u SET "ProfileStep" = CASE
                    WHEN u."ProfileCompleted" THEN 'done'
                    WHEN EXISTS (SELECT 1 FROM "UserProfiles" p WHERE p."UserId" = u."Id")
                         AND EXISTS (SELECT 1 FROM "UserAddresses" a WHERE a."UserId" = u."Id") THEN 'documents'
                    WHEN EXISTS (SELECT 1 FROM "UserProfiles" p WHERE p."UserId" = u."Id") THEN 'address'
                    ELSE 'personal' END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceCategories");

            migrationBuilder.DropColumn(
                name: "ProfileStep",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UploadedAt",
                table: "UserDocuments");

            migrationBuilder.DropColumn(
                name: "District",
                table: "UserAddresses");

            migrationBuilder.DropColumn(
                name: "Province",
                table: "UserAddresses");
        }
    }
}
