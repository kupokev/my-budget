using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AiApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiApiKey",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiApiKey",
                table: "AppSettings");
        }
    }
}
