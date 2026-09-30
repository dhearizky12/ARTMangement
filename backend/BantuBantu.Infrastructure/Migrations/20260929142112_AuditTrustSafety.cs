using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BantuBantu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrustSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActorAgencyId",
                table: "AuditEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorRole",
                table: "AuditEntries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "AuditEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetEntityId",
                table: "AuditEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetEntityType",
                table: "AuditEntries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_CreatedAt",
                table: "AuditEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TargetEntityType_TargetEntityId",
                table: "AuditEntries",
                columns: new[] { "TargetEntityType", "TargetEntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_CreatedAt",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_TargetEntityType_TargetEntityId",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "ActorAgencyId",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "ActorRole",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "TargetEntityId",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "TargetEntityType",
                table: "AuditEntries");
        }
    }
}
