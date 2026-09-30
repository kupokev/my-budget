using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Data.Migrations
{
    /// <inheritdoc />
    public partial class IncomeTaggedTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IncomeSourceId",
                table: "CategoryRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_IncomeSourceId",
                table: "CategoryRules",
                column: "IncomeSourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_CategoryRules_IncomeSources_IncomeSourceId",
                table: "CategoryRules",
                column: "IncomeSourceId",
                principalTable: "IncomeSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CategoryRules_IncomeSources_IncomeSourceId",
                table: "CategoryRules");

            migrationBuilder.DropIndex(
                name: "IX_CategoryRules_IncomeSourceId",
                table: "CategoryRules");

            migrationBuilder.DropColumn(
                name: "IncomeSourceId",
                table: "CategoryRules");
        }
    }
}
