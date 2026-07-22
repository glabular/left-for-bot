using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace left_4_bot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SteamId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: true),
                    TelegramUsername = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    IsPriority = table.Column<bool>(type: "INTEGER", nullable: false),
                    LinkedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Players_SteamId",
                table: "Players",
                column: "SteamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_TelegramUserId",
                table: "Players",
                column: "TelegramUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Players");
        }
    }
}
