using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KleinGeld.Data.Migrations
{
    /// <inheritdoc />
    public partial class RightsDeclarations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RightsDeclarations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReleaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    MadeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WordingVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RightsDeclarations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RightsDeclarations_AspNetUsers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RightsDeclarations_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RightsDeclarations_MemberId",
                table: "RightsDeclarations",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RightsDeclarations_ReleaseId",
                table: "RightsDeclarations",
                column: "ReleaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RightsDeclarations");
        }
    }
}
