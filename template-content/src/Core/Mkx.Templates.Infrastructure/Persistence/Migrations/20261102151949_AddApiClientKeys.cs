using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mkx.Templates.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApiClientKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiClientKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Scopes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiClientKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiClientKeys_CreatedById",
                table: "ApiClientKeys",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ApiClientKeys_ExpiresAt",
                table: "ApiClientKeys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApiClientKeys_Prefix",
                table: "ApiClientKeys",
                column: "Prefix");

            migrationBuilder.CreateIndex(
                name: "IX_ApiClientKeys_TokenHash",
                table: "ApiClientKeys",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiClientKeys");
        }
    }
}
