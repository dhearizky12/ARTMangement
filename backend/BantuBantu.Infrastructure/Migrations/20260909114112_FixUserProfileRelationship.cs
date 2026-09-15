using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BantuBantu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixUserProfileRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_Users_UserId1",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_UserId1",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "UserProfiles");

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_Users_UserId",
                table: "UserProfiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_Users_UserId",
                table: "UserProfiles");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "UserProfiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE \"UserProfiles\" SET \"UserId1\" = \"UserId\"");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId1",
                table: "UserProfiles",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_Users_UserId1",
                table: "UserProfiles",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
