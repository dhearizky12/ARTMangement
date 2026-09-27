using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BantuBantu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProviderSelfRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationStatus",
                table: "Providers",
                type: "text",
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<string>(
                name: "ModerationNote",
                table: "Providers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "Providers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                table: "Providers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "Providers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationStatus",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "ModerationNote",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Providers");
        }
    }
}
