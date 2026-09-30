using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Data.Migrations
{
    /// <inheritdoc />
    public partial class DepositSplits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IncomeSourceId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DepositSplit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IncomeSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    IsRemainder = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepositSplit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepositSplit_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepositSplit_IncomeSources_IncomeSourceId",
                        column: x => x.IncomeSourceId,
                        principalTable: "IncomeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_IncomeSourceId",
                table: "Transactions",
                column: "IncomeSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositSplit_AccountId",
                table: "DepositSplit",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositSplit_IncomeSourceId",
                table: "DepositSplit",
                column: "IncomeSourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_IncomeSources_IncomeSourceId",
                table: "Transactions",
                column: "IncomeSourceId",
                principalTable: "IncomeSources",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_IncomeSources_IncomeSourceId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "DepositSplit");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_IncomeSourceId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IncomeSourceId",
                table: "Transactions");
        }
    }
}
