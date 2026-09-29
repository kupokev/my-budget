using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Data.Migrations
{
    /// <inheritdoc />
    public partial class EarnedPerks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxUsesPerPeriod",
                table: "CardPerk",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Period",
                table: "CardPerk",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ValuePerUse",
                table: "CardPerk",
                type: "TEXT",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CardPerkUse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardPerkId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardPerkUse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardPerkUse_CardPerk_CardPerkId",
                        column: x => x.CardPerkId,
                        principalTable: "CardPerk",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardPerkUse_CardPerkId_Date",
                table: "CardPerkUse",
                columns: new[] { "CardPerkId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardPerkUse");

            migrationBuilder.DropColumn(
                name: "MaxUsesPerPeriod",
                table: "CardPerk");

            migrationBuilder.DropColumn(
                name: "Period",
                table: "CardPerk");

            migrationBuilder.DropColumn(
                name: "ValuePerUse",
                table: "CardPerk");
        }
    }
}
