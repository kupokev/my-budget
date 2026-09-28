using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Data.Migrations
{
    /// <inheritdoc />
    public partial class CardNicknameAndFeeBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BudgetAnnualFee",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FeeBudgetLineId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_FeeBudgetLineId",
                table: "Cards",
                column: "FeeBudgetLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_BudgetLines_FeeBudgetLineId",
                table: "Cards",
                column: "FeeBudgetLineId",
                principalTable: "BudgetLines",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_BudgetLines_FeeBudgetLineId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_FeeBudgetLineId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "BudgetAnnualFee",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "FeeBudgetLineId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "Cards");
        }
    }
}
