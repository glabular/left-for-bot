using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace left_4_bot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerToteBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Balance",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StarterGrantedAt",
                table: "Players",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "StarterGrantedAt",
                table: "Players");
        }
    }
}
