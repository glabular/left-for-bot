using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace left_4_bot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyBetsAndMarkets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyBets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GamingDayAnchor = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    TargetSteamId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyBets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyMarkets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GamingDayAnchor = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyMarkets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyBets_GamingDayAnchor_TelegramUserId",
                table: "DailyBets",
                columns: new[] { "GamingDayAnchor", "TelegramUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyMarkets_GamingDayAnchor",
                table: "DailyMarkets",
                column: "GamingDayAnchor",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyBets");

            migrationBuilder.DropTable(
                name: "DailyMarkets");
        }
    }
}
