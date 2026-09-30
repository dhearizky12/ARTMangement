using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BantuBantu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReviewModerationAndTrustCms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_ProviderId",
                table: "Reviews");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HiddenAt",
                table: "Reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HiddenBy",
                table: "Reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenReason",
                table: "Reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Reviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IconName",
                table: "ContentBlocks",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ContentBlocks",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "ContentBlocks",
                keyColumn: "Id",
                keyValue: "verification",
                columns: new[] { "Body", "IconName", "IsActive", "SortOrder" },
                values: new object[] { "Admin memeriksa identitas, latar belakang, dan kontrak sebelum profil penyedia diterbitkan.", "shield-check", true, 6 });

            migrationBuilder.InsertData(
                table: "ContentBlocks",
                columns: new[] { "Id", "Body", "IconName", "IsActive", "SortOrder", "Title" },
                values: new object[,]
                {
                    { "trust-background", "Tim kami meninjau informasi latar belakang dan pengalaman kerja sebelum profil diterbitkan.", "search-check", true, 3, "Latar belakang ditinjau" },
                    { "trust-guarantee", "Lencana terverifikasi menunjukkan pemeriksaan telah selesai. Anda tetap dapat melihat detail profil, harga, ketersediaan, dan ulasan sebelum memesan.", "sparkles", true, 4, "Jaminan proses yang transparan" },
                    { "trust-identity", "Kami memeriksa dokumen identitas penyedia dan mencocokkannya dengan data pendaftaran.", "badge-check", true, 2, "Identitas diperiksa" },
                    { "trust-intro", "Kami menjelaskan bagaimana penyedia jasa diperiksa sebelum Anda memesan layanan.", "shield-check", true, 1, "Kepercayaan dimulai dari kejelasan." },
                    { "trust-protection", "Pesan melalui platform, simpan detail pesanan, dan laporkan masalah kepada tim Bantu-Bantu agar dapat ditindaklanjuti.", "heart-handshake", true, 5, "Perlindungan pelanggan" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ProviderId_IsHidden_CreatedAt",
                table: "Reviews",
                columns: new[] { "ProviderId", "IsHidden", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_ProviderId_IsHidden_CreatedAt",
                table: "Reviews");

            migrationBuilder.DeleteData(
                table: "ContentBlocks",
                keyColumn: "Id",
                keyValue: "trust-background");

            migrationBuilder.DeleteData(
                table: "ContentBlocks",
                keyColumn: "Id",
                keyValue: "trust-guarantee");

            migrationBuilder.DeleteData(
                table: "ContentBlocks",
                keyColumn: "Id",
                keyValue: "trust-identity");

            migrationBuilder.DeleteData(
                table: "ContentBlocks",
                keyColumn: "Id",
                keyValue: "trust-intro");

            migrationBuilder.DeleteData(
                table: "ContentBlocks",
                keyColumn: "Id",
                keyValue: "trust-protection");

            migrationBuilder.DropColumn(
                name: "HiddenAt",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "HiddenBy",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "HiddenReason",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "IconName",
                table: "ContentBlocks");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ContentBlocks");

            migrationBuilder.UpdateData(
                table: "ContentBlocks",
                keyColumn: "Id",
                keyValue: "verification",
                columns: new[] { "Body", "SortOrder" },
                values: new object[] { "Admin memeriksa identitas, latar belakang, dan kontrak sebelum profil penyedia diterbitkan. Lencana terverifikasi menunjukkan pemeriksaan tersebut telah selesai, bukan jaminan atas setiap hasil layanan.", 1 });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ProviderId",
                table: "Reviews",
                column: "ProviderId");
        }
    }
}
