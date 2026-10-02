using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Data.Migrations
{
    /// <inheritdoc />
    public partial class BudgetPeriodConfirmationNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfirmationNumber",
                table: "BudgetPeriods",
                type: "TEXT",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmationNumber",
                table: "BudgetPeriods");
        }
    }
}
