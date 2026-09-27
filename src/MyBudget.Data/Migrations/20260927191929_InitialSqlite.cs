using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Institution = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountNumber = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    MinimumBalance = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    TransferCadence = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRainyDayFund = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCardEligible = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContributionLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    HsaSelfOnly = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    HsaFamily = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    HsaCatchUp = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Retirement401kEmployee = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Retirement401kCatchUp = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Retirement401kTotal = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Ira = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    IraCatchUp = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: true),
                    Verified = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContributionLimits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EstimatedTaxPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Jurisdiction = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstimatedTaxPayments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Goals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Metric = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    StartValue = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ManualCurrent = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    AccountIds = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AccountType = table.Column<int>(type: "INTEGER", nullable: true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    LoanId = table.Column<int>(type: "INTEGER", nullable: true),
                    LowerIsBetter = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HsaYears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    TargetDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CatchUpEligible = table.Column<bool>(type: "INTEGER", nullable: false),
                    LimitOverrideSelfOnly = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    LimitOverrideFamily = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsaYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncomeSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyPrograms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PointValueCents = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    PointsBalance = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    CurrentTier = table.Column<string>(type: "TEXT", nullable: true),
                    TargetTier = table.Column<string>(type: "TEXT", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyPrograms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxYears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    FederalStandardDeductionSingle = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    FederalStandardDeductionMarriedJointly = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    FederalStandardDeductionHeadOfHousehold = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    SocialSecurityRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    SocialSecurityWageBase = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    MedicareRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    AdditionalMedicareRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    AdditionalMedicareThreshold = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    SupplementalRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    SupplementalHighRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    SupplementalHighThreshold = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    MissouriStandardDeductionSingle = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    MissouriStandardDeductionMarriedSpouseWorks = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    MissouriStandardDeductionMarriedOneIncome = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    MissouriStandardDeductionHeadOfHousehold = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    MissouriSupplementalRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    LtcgThreshold15Single = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    LtcgThreshold15MarriedJointly = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    LtcgThreshold15HeadOfHousehold = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    LtcgThreshold20Single = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    LtcgThreshold20MarriedJointly = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    LtcgThreshold20HeadOfHousehold = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: true),
                    Verified = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    AsOf = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Balance = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountBalances_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Holdings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Drip = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holdings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holdings_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetValue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    AsOf = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetValue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetValue_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Labels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Labels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Labels_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HsaContribution",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HsaYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsaContribution", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HsaContribution_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HsaContribution_HsaYears_HsaYearId",
                        column: x => x.HsaYearId,
                        principalTable: "HsaYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HsaMonth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HsaYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<int>(type: "INTEGER", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsaMonth", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HsaMonth_HsaYears_HsaYearId",
                        column: x => x.HsaYearId,
                        principalTable: "HsaYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeductionElections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IncomeSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Treatment = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountPerCheck = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    PercentOfGross = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeductionElections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeductionElections_IncomeSources_IncomeSourceId",
                        column: x => x.IncomeSourceId,
                        principalTable: "IncomeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncomeReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IncomeSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeReceipts_IncomeSources_IncomeSourceId",
                        column: x => x.IncomeSourceId,
                        principalTable: "IncomeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaycheckOverride",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IncomeSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    PayDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    GrossFraction = table.Column<decimal>(type: "TEXT", precision: 6, scale: 4, nullable: true),
                    GrossAmount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    ProrateFixedDeductions = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaycheckOverride", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaycheckOverride_IncomeSources_IncomeSourceId",
                        column: x => x.IncomeSourceId,
                        principalTable: "IncomeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Paychecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IncomeSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    PayDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Gross = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Net = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paychecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Paychecks_IncomeSources_IncomeSourceId",
                        column: x => x.IncomeSourceId,
                        principalTable: "IncomeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaySchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IncomeSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Frequency = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AnchorPayDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PayOnPriorBusinessDay = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstPayDay = table.Column<int>(type: "INTEGER", nullable: true),
                    SecondPayDay = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaySchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaySchedules_IncomeSources_IncomeSourceId",
                        column: x => x.IncomeSourceId,
                        principalTable: "IncomeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalaryRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IncomeSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    AnnualAmount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryRates_IncomeSources_IncomeSourceId",
                        column: x => x.IncomeSourceId,
                        principalTable: "IncomeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WithholdingElections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IncomeSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    FederalStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    MultipleJobs = table.Column<bool>(type: "INTEGER", nullable: false),
                    DependentCredits = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    OtherIncome = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Deductions = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    ExtraWithholding = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    MissouriStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    MissouriExtraWithholding = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithholdingElections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WithholdingElections_IncomeSources_IncomeSourceId",
                        column: x => x.IncomeSourceId,
                        principalTable: "IncomeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Issuer = table.Column<string>(type: "TEXT", nullable: true),
                    Network = table.Column<string>(type: "TEXT", nullable: true),
                    AccountNumber = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Apr = table.Column<decimal>(type: "TEXT", precision: 6, scale: 3, nullable: false),
                    PromoApr = table.Column<decimal>(type: "TEXT", precision: 6, scale: 3, nullable: true),
                    PromoAprExpires = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    StatementDay = table.Column<int>(type: "INTEGER", nullable: false),
                    DueDay = table.Column<int>(type: "INTEGER", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    AnnualFee = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    AnnualFeeMonth = table.Column<int>(type: "INTEGER", nullable: true),
                    PayingAccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LoyaltyProgramId = table.Column<int>(type: "INTEGER", nullable: true),
                    PointValueCents = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cards_Accounts_PayingAccountId",
                        column: x => x.PayingAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Cards_LoyaltyPrograms_LoyaltyProgramId",
                        column: x => x.LoyaltyProgramId,
                        principalTable: "LoyaltyPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyProgress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProgramId = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Nights = table.Column<int>(type: "INTEGER", nullable: false),
                    Stays = table.Column<int>(type: "INTEGER", nullable: false),
                    ProgramSpend = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    QualifyingPoints = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyProgress_LoyaltyPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "LoyaltyPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyTier",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProgramId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    Benefits = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTier", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyTier_LoyaltyPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "LoyaltyPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivableCharge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivableCharge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivableCharge_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivablePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivablePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivablePayments_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxBracket",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaxYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    Jurisdiction = table.Column<int>(type: "INTEGER", nullable: false),
                    FilingStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    Over = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxBracket", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxBracket_TaxYears_TaxYearId",
                        column: x => x.TaxYearId,
                        principalTable: "TaxYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Dividends",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HoldingId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PayDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PerShare = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    SharesHeld = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Reinvested = table.Column<bool>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dividends", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dividends_Holdings_HoldingId",
                        column: x => x.HoldingId,
                        principalTable: "Holdings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaycheckLine",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PaycheckId = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaycheckLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaycheckLine_Paychecks_PaycheckId",
                        column: x => x.PaycheckId,
                        principalTable: "Paychecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BudgetLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    LabelId = table.Column<int>(type: "INTEGER", nullable: true),
                    AccountNumber = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Frequency = table.Column<int>(type: "INTEGER", nullable: false),
                    DueDay = table.Column<int>(type: "INTEGER", nullable: false),
                    AnchorDueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    IsAutopay = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProjectedAmount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    PaymentMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentAccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    PaymentCardId = table.Column<int>(type: "INTEGER", nullable: true),
                    FundingAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    BankAutopayDiscount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    IsCardEligible = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetLines_Accounts_FundingAccountId",
                        column: x => x.FundingAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetLines_Accounts_PaymentAccountId",
                        column: x => x.PaymentAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetLines_Cards_PaymentCardId",
                        column: x => x.PaymentCardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetLines_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BudgetLines_Labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CardBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    AsOf = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Balance = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardBalances_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CardPerk",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AnnualValue = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    StartYear = table.Column<int>(type: "INTEGER", nullable: true),
                    EndYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardPerk", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardPerk_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EarnRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    LabelId = table.Column<int>(type: "INTEGER", nullable: true),
                    PointsPerDollar = table.Column<decimal>(type: "TEXT", precision: 8, scale: 3, nullable: false),
                    AnnualSpendCap = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    StartYear = table.Column<int>(type: "INTEGER", nullable: true),
                    EndYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarnRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EarnRules_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EarnRules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EarnRules_Labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    Format = table.Column<int>(type: "INTEGER", nullable: false),
                    Profile = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: true),
                    RowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DuplicateCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    LastDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportBatches_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ImportBatches_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SpendThresholds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    RewardKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ValueDollars = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    TierName = table.Column<string>(type: "TEXT", nullable: true),
                    StartYear = table.Column<int>(type: "INTEGER", nullable: true),
                    EndYear = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpendThresholds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpendThresholds_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StatusPath",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProgramId = table.Column<int>(type: "INTEGER", nullable: false),
                    TierName = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Threshold = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    CardId = table.Column<int>(type: "INTEGER", nullable: true),
                    StartYear = table.Column<int>(type: "INTEGER", nullable: true),
                    EndYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusPath", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatusPath_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StatusPath_LoyaltyPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "LoyaltyPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PaymentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Period = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAllocation_ReceivablePayments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "ReceivablePayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HoldingId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Shares = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Fees = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    DividendPaymentId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trades_Dividends_DividendPaymentId",
                        column: x => x.DividendPaymentId,
                        principalTable: "Dividends",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Trades_Holdings_HoldingId",
                        column: x => x.HoldingId,
                        principalTable: "Holdings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BudgetPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BudgetLineId = table.Column<int>(type: "INTEGER", nullable: false),
                    Period = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ProjectedAmount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    ActualAmount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    PaidOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetPeriods_BudgetLines_BudgetLineId",
                        column: x => x.BudgetLineId,
                        principalTable: "BudgetLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoryRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Pattern = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Match = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    LabelId = table.Column<int>(type: "INTEGER", nullable: true),
                    BudgetLineId = table.Column<int>(type: "INTEGER", nullable: true),
                    MarkAsTransfer = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryRules_BudgetLines_BudgetLineId",
                        column: x => x.BudgetLineId,
                        principalTable: "BudgetLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryRules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryRules_Labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Loans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Lender = table.Column<string>(type: "TEXT", nullable: true),
                    AccountNumber = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    OriginalPrincipal = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    AnnualRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    TermMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ScheduledPayment = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    ExtraMonthlyPayment = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    BudgetLineId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Loans_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Loans_BudgetLines_BudgetLineId",
                        column: x => x.BudgetLineId,
                        principalTable: "BudgetLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Obligation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    BudgetLineId = table.Column<int>(type: "INTEGER", nullable: true),
                    ShareOfLine = table.Column<decimal>(type: "TEXT", precision: 6, scale: 4, nullable: false),
                    StartPeriod = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndPeriod = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EveryMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Obligation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Obligation_BudgetLines_BudgetLineId",
                        column: x => x.BudgetLineId,
                        principalTable: "BudgetLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Obligation_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PostedDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Merchant = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    LabelId = table.Column<int>(type: "INTEGER", nullable: true),
                    BudgetLineId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsTransfer = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ImportBatchId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsManuallyCategorized = table.Column<bool>(type: "INTEGER", nullable: false),
                    Origin = table.Column<int>(type: "INTEGER", nullable: false),
                    CounterpartyAccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    LinkedTransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReconciledWithId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReceivablePaymentId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_CounterpartyAccountId",
                        column: x => x.CounterpartyAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transactions_BudgetLines_BudgetLineId",
                        column: x => x.BudgetLineId,
                        principalTable: "BudgetLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transactions_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transactions_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transactions_Labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transactions_ReceivablePayments_ReceivablePaymentId",
                        column: x => x.ReceivablePaymentId,
                        principalTable: "ReceivablePayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transactions_Transactions_ReconciledWithId",
                        column: x => x.ReconciledWithId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LoanBalance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LoanId = table.Column<int>(type: "INTEGER", nullable: false),
                    AsOf = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Balance = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanBalance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanBalance_Loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_AccountId_AsOf",
                table: "AccountBalances",
                columns: new[] { "AccountId", "AsOf" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetValue_AssetId_AsOf",
                table: "AssetValue",
                columns: new[] { "AssetId", "AsOf" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_CategoryId",
                table: "BudgetLines",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_FundingAccountId",
                table: "BudgetLines",
                column: "FundingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_LabelId",
                table: "BudgetLines",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_PaymentAccountId",
                table: "BudgetLines",
                column: "PaymentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_PaymentCardId",
                table: "BudgetLines",
                column: "PaymentCardId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_BudgetLineId_Period",
                table: "BudgetPeriods",
                columns: new[] { "BudgetLineId", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardBalances_CardId_AsOf",
                table: "CardBalances",
                columns: new[] { "CardId", "AsOf" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardPerk_CardId",
                table: "CardPerk",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_LoyaltyProgramId",
                table: "Cards",
                column: "LoyaltyProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_PayingAccountId",
                table: "Cards",
                column: "PayingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_BudgetLineId",
                table: "CategoryRules",
                column: "BudgetLineId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_CategoryId",
                table: "CategoryRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_LabelId",
                table: "CategoryRules",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_ContributionLimits_Year",
                table: "ContributionLimits",
                column: "Year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeductionElections_IncomeSourceId",
                table: "DeductionElections",
                column: "IncomeSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Dividends_HoldingId_ExDate",
                table: "Dividends",
                columns: new[] { "HoldingId", "ExDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EarnRules_CardId",
                table: "EarnRules",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_EarnRules_CategoryId",
                table: "EarnRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EarnRules_LabelId",
                table: "EarnRules",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_Holdings_AccountId_Ticker",
                table: "Holdings",
                columns: new[] { "AccountId", "Ticker" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsaContribution_AccountId",
                table: "HsaContribution",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_HsaContribution_HsaYearId",
                table: "HsaContribution",
                column: "HsaYearId");

            migrationBuilder.CreateIndex(
                name: "IX_HsaMonth_HsaYearId_Month",
                table: "HsaMonth",
                columns: new[] { "HsaYearId", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsaYears_Year",
                table: "HsaYears",
                column: "Year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_AccountId",
                table: "ImportBatches",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_CardId",
                table: "ImportBatches",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeReceipts_IncomeSourceId",
                table: "IncomeReceipts",
                column: "IncomeSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Labels_CategoryId",
                table: "Labels",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Labels_Name",
                table: "Labels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanBalance_LoanId_AsOf",
                table: "LoanBalance",
                columns: new[] { "LoanId", "AsOf" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loans_AssetId",
                table: "Loans",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Loans_BudgetLineId",
                table: "Loans",
                column: "BudgetLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyProgress_ProgramId_Year",
                table: "LoyaltyProgress",
                columns: new[] { "ProgramId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTier_ProgramId",
                table: "LoyaltyTier",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_Obligation_BudgetLineId",
                table: "Obligation",
                column: "BudgetLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Obligation_PersonId",
                table: "Obligation",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PaycheckLine_PaycheckId",
                table: "PaycheckLine",
                column: "PaycheckId");

            migrationBuilder.CreateIndex(
                name: "IX_PaycheckOverride_IncomeSourceId_PayDate",
                table: "PaycheckOverride",
                columns: new[] { "IncomeSourceId", "PayDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Paychecks_IncomeSourceId_PayDate_Kind",
                table: "Paychecks",
                columns: new[] { "IncomeSourceId", "PayDate", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocation_PaymentId",
                table: "PaymentAllocation",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaySchedules_IncomeSourceId_EffectiveDate",
                table: "PaySchedules",
                columns: new[] { "IncomeSourceId", "EffectiveDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prices_Ticker_Date",
                table: "Prices",
                columns: new[] { "Ticker", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceivableCharge_PersonId",
                table: "ReceivableCharge",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivablePayments_PersonId",
                table: "ReceivablePayments",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRates_IncomeSourceId_EffectiveDate",
                table: "SalaryRates",
                columns: new[] { "IncomeSourceId", "EffectiveDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpendThresholds_CardId",
                table: "SpendThresholds",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_StatusPath_CardId",
                table: "StatusPath",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_StatusPath_ProgramId",
                table: "StatusPath",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxBracket_TaxYearId",
                table: "TaxBracket",
                column: "TaxYearId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxYears_Year",
                table: "TaxYears",
                column: "Year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trades_DividendPaymentId",
                table: "Trades",
                column: "DividendPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_HoldingId",
                table: "Trades",
                column: "HoldingId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId_CardId_ExternalId",
                table: "Transactions",
                columns: new[] { "AccountId", "CardId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BudgetLineId",
                table: "Transactions",
                column: "BudgetLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CardId",
                table: "Transactions",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CategoryId",
                table: "Transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CounterpartyAccountId",
                table: "Transactions",
                column: "CounterpartyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Date",
                table: "Transactions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ImportBatchId",
                table: "Transactions",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_LabelId",
                table: "Transactions",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ReceivablePaymentId",
                table: "Transactions",
                column: "ReceivablePaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ReconciledWithId",
                table: "Transactions",
                column: "ReconciledWithId");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingElections_IncomeSourceId_EffectiveDate",
                table: "WithholdingElections",
                columns: new[] { "IncomeSourceId", "EffectiveDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountBalances");

            migrationBuilder.DropTable(
                name: "AssetValue");

            migrationBuilder.DropTable(
                name: "BudgetPeriods");

            migrationBuilder.DropTable(
                name: "CardBalances");

            migrationBuilder.DropTable(
                name: "CardPerk");

            migrationBuilder.DropTable(
                name: "CategoryRules");

            migrationBuilder.DropTable(
                name: "ContributionLimits");

            migrationBuilder.DropTable(
                name: "DeductionElections");

            migrationBuilder.DropTable(
                name: "EarnRules");

            migrationBuilder.DropTable(
                name: "EstimatedTaxPayments");

            migrationBuilder.DropTable(
                name: "Goals");

            migrationBuilder.DropTable(
                name: "HsaContribution");

            migrationBuilder.DropTable(
                name: "HsaMonth");

            migrationBuilder.DropTable(
                name: "IncomeReceipts");

            migrationBuilder.DropTable(
                name: "LoanBalance");

            migrationBuilder.DropTable(
                name: "LoyaltyProgress");

            migrationBuilder.DropTable(
                name: "LoyaltyTier");

            migrationBuilder.DropTable(
                name: "Obligation");

            migrationBuilder.DropTable(
                name: "PaycheckLine");

            migrationBuilder.DropTable(
                name: "PaycheckOverride");

            migrationBuilder.DropTable(
                name: "PaymentAllocation");

            migrationBuilder.DropTable(
                name: "PaySchedules");

            migrationBuilder.DropTable(
                name: "Prices");

            migrationBuilder.DropTable(
                name: "ReceivableCharge");

            migrationBuilder.DropTable(
                name: "SalaryRates");

            migrationBuilder.DropTable(
                name: "SpendThresholds");

            migrationBuilder.DropTable(
                name: "StatusPath");

            migrationBuilder.DropTable(
                name: "TaxBracket");

            migrationBuilder.DropTable(
                name: "Trades");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "WithholdingElections");

            migrationBuilder.DropTable(
                name: "HsaYears");

            migrationBuilder.DropTable(
                name: "Loans");

            migrationBuilder.DropTable(
                name: "Paychecks");

            migrationBuilder.DropTable(
                name: "TaxYears");

            migrationBuilder.DropTable(
                name: "Dividends");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "ReceivablePayments");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "BudgetLines");

            migrationBuilder.DropTable(
                name: "IncomeSources");

            migrationBuilder.DropTable(
                name: "Holdings");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.DropTable(
                name: "Cards");

            migrationBuilder.DropTable(
                name: "Labels");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "LoyaltyPrograms");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
