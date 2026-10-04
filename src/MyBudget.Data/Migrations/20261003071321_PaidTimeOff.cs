using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Data.Migrations
{
    /// <inheritdoc />
    public partial class PaidTimeOff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TimeOffBucketId",
                table: "Goals",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TimeOffHours",
                table: "Goals",
                type: "TEXT",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TimeOffStarts",
                table: "Goals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TimeOffBuckets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IncomeSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    AccrualHoursPerPaycheck = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    AnnualGrantHours = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    GrantMonth = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxHours = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    HoursPerDay = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeOffBuckets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeOffBuckets_IncomeSources_IncomeSourceId",
                        column: x => x.IncomeSourceId,
                        principalTable: "IncomeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaycheckTimeOff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PaycheckId = table.Column<int>(type: "INTEGER", nullable: false),
                    BucketId = table.Column<int>(type: "INTEGER", nullable: false),
                    Accrued = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    Used = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    Balance = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaycheckTimeOff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaycheckTimeOff_Paychecks_PaycheckId",
                        column: x => x.PaycheckId,
                        principalTable: "Paychecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaycheckTimeOff_TimeOffBuckets_BucketId",
                        column: x => x.BucketId,
                        principalTable: "TimeOffBuckets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaycheckTimeOff_BucketId",
                table: "PaycheckTimeOff",
                column: "BucketId");

            migrationBuilder.CreateIndex(
                name: "IX_PaycheckTimeOff_PaycheckId_BucketId",
                table: "PaycheckTimeOff",
                columns: new[] { "PaycheckId", "BucketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffBuckets_IncomeSourceId",
                table: "TimeOffBuckets",
                column: "IncomeSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaycheckTimeOff");

            migrationBuilder.DropTable(
                name: "TimeOffBuckets");

            migrationBuilder.DropColumn(
                name: "TimeOffBucketId",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "TimeOffHours",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "TimeOffStarts",
                table: "Goals");
        }
    }
}
