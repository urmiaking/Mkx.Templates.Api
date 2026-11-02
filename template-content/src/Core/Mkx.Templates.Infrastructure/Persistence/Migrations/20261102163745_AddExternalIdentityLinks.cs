using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mkx.Templates.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIdentityLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalIdentityLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Issuer = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LinkedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIdentityLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalIdentityLinks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalLinkChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalLinkChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalLinkChallenges_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinks_Issuer_Subject",
                table: "ExternalIdentityLinks",
                columns: new[] { "Issuer", "Subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinks_UserId_Issuer",
                table: "ExternalIdentityLinks",
                columns: new[] { "UserId", "Issuer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLinkChallenges_ExpiresAt",
                table: "ExternalLinkChallenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLinkChallenges_TokenHash",
                table: "ExternalLinkChallenges",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLinkChallenges_UserId",
                table: "ExternalLinkChallenges",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalIdentityLinks");

            migrationBuilder.DropTable(
                name: "ExternalLinkChallenges");
        }
    }
}
