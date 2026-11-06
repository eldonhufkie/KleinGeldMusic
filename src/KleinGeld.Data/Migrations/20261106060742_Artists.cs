using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KleinGeld.Data.Migrations
{
    /// <inheritdoc />
    public partial class Artists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Artists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArtistManagers",
                columns: table => new
                {
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagersId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistManagers", x => new { x.ArtistId, x.ManagersId });
                    table.ForeignKey(
                        name: "FK_ArtistManagers_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtistManagers_AspNetUsers_ManagersId",
                        column: x => x.ManagersId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtistManagers_ManagersId",
                table: "ArtistManagers",
                column: "ManagersId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtistManagers");

            migrationBuilder.DropTable(
                name: "Artists");
        }
    }
}
