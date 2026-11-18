using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KleinGeld.Data.Migrations
{
    /// <inheritdoc />
    public partial class TagPreFill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TitleTyped",
                table: "Tracks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ReleaseDate",
                table: "Releases",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TitleTyped",
                table: "Tracks");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ReleaseDate",
                table: "Releases",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
