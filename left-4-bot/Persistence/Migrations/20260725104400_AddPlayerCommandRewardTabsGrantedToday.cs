using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace left_4_bot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerCommandRewardTabsGrantedToday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommandRewardTabsGrantedToday",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommandRewardTabsGrantedToday",
                table: "Players");
        }
    }
}
