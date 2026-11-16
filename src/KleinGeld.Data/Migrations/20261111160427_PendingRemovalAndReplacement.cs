using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KleinGeld.Data.Migrations
{
    /// <inheritdoc />
    public partial class PendingRemovalAndReplacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LiveConvertedBlobName",
                table: "Tracks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "LiveDuration",
                table: "Tracks",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiveOriginalBlobName",
                table: "Tracks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PendingRemoval",
                table: "Tracks",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LiveConvertedBlobName",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "LiveDuration",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "LiveOriginalBlobName",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "PendingRemoval",
                table: "Tracks");
        }
    }
}
