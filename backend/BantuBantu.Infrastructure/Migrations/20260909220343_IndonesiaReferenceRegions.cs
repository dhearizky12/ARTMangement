using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BantuBantu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IndonesiaReferenceRegions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AddColumn<string>(
                name: "VillageId",
                table: "UserAddresses",
                type: "character varying(10)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Provinces",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Regencies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ProvinceId = table.Column<string>(type: "character varying(2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Regencies_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Districts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    RegencyId = table.Column<string>(type: "character varying(4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Districts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Districts_Regencies_RegencyId",
                        column: x => x.RegencyId,
                        principalTable: "Regencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Villages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DistrictId = table.Column<string>(type: "character varying(6)", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Villages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Villages_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAddresses_VillageId",
                table: "UserAddresses",
                column: "VillageId");

            migrationBuilder.CreateIndex(
                name: "IX_Districts_RegencyId",
                table: "Districts",
                column: "RegencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Regencies_ProvinceId",
                table: "Regencies",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Villages_DistrictId",
                table: "Villages",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Villages_Name",
                table: "Villages",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Villages_Name_Trgm",
                table: "Villages",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserAddresses_Villages_VillageId",
                table: "UserAddresses",
                column: "VillageId",
                principalTable: "Villages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserAddresses_Villages_VillageId",
                table: "UserAddresses");

            migrationBuilder.DropTable(
                name: "Villages");

            migrationBuilder.DropTable(
                name: "Districts");

            migrationBuilder.DropTable(
                name: "Regencies");

            migrationBuilder.DropTable(
                name: "Provinces");

            migrationBuilder.DropIndex(
                name: "IX_UserAddresses_VillageId",
                table: "UserAddresses");

            migrationBuilder.DropColumn(
                name: "VillageId",
                table: "UserAddresses");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
