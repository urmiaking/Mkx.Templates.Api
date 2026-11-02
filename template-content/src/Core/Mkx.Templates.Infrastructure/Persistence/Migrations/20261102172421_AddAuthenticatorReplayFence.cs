using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mkx.Templates.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticatorReplayFence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RedeemedAuthenticatorCodes",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Digest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedeemedAuthenticatorCodes", x => new { x.UserId, x.Digest });
                    table.ForeignKey(
                        name: "FK_RedeemedAuthenticatorCodes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RedeemedAuthenticatorCodes_ExpiresAtUtc",
                table: "RedeemedAuthenticatorCodes",
                column: "ExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RedeemedAuthenticatorCodes");
        }
    }
}
