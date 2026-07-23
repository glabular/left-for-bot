using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace left_4_bot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerCommandRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommandRewardCount",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CommandRewardGamingDay",
                table: "Players",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommandRewardCount",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CommandRewardGamingDay",
                table: "Players");
        }
    }
}
