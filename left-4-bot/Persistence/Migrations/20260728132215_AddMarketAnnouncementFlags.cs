using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace left_4_bot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketAnnouncementFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CloseAnnounced",
                table: "DailyMarkets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OpenAnnounced",
                table: "DailyMarkets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderAnnounced",
                table: "DailyMarkets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SettlementAnnounced",
                table: "DailyMarkets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CloseAnnounced",
                table: "DailyMarkets");

            migrationBuilder.DropColumn(
                name: "OpenAnnounced",
                table: "DailyMarkets");

            migrationBuilder.DropColumn(
                name: "ReminderAnnounced",
                table: "DailyMarkets");

            migrationBuilder.DropColumn(
                name: "SettlementAnnounced",
                table: "DailyMarkets");
        }
    }
}
