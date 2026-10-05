using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Data.Migrations
{
    /// <inheritdoc />
    public partial class RowTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "WithholdingElections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "WithholdingElections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Trades",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Trades",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TimeOffBuckets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TimeOffBuckets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TaxYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TaxYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TaxBracket",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TaxBracket",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "StatusPath",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "StatusPath",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "SpendThresholds",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SpendThresholds",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "SalaryRates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SalaryRates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ReceivablePayments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ReceivablePayments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ReceivableCharge",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ReceivableCharge",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Prices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Prices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "People",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "People",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "PaySchedules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PaySchedules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "PaymentAllocation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PaymentAllocation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "PaycheckTimeOff",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PaycheckTimeOff",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Paychecks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Paychecks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "PaycheckOverride",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PaycheckOverride",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "PaycheckLine",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PaycheckLine",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Obligation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Obligation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "LoyaltyTier",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "LoyaltyTier",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "LoyaltyProgress",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "LoyaltyProgress",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "LoyaltyPrograms",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "LoyaltyPrograms",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Loans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Loans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "LoanBalance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "LoanBalance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Labels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Labels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "InvestmentFees",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "InvestmentFees",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "InvestmentContributions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "InvestmentContributions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "IncomeSources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "IncomeSources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "IncomeReceipts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "IncomeReceipts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ImportBatches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ImportBatches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "HsaYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "HsaYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "HsaMonth",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "HsaMonth",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "HsaContribution",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "HsaContribution",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Holdings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Holdings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Goals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Goals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "EstimatedTaxPayments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "EstimatedTaxPayments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "EarnRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "EarnRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Dividends",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Dividends",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "DepositSplit",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "DepositSplit",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "DeductionElections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "DeductionElections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ContributionLimits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ContributionLimits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CategoryRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CategoryRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Categories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Categories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CardPerkUse",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CardPerkUse",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CardPerk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CardPerk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CardFee",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CardFee",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CardBalances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CardBalances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "BudgetPeriods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "BudgetPeriods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "BudgetLines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "BudgetLines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "BudgetLineAmount",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "BudgetLineAmount",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AssetValue",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AssetValue",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AccountBalances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AccountBalances",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "WithholdingElections");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "WithholdingElections");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TimeOffBuckets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TimeOffBuckets");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TaxYears");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TaxYears");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TaxBracket");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TaxBracket");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "StatusPath");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "StatusPath");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "SpendThresholds");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SpendThresholds");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "SalaryRates");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SalaryRates");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ReceivablePayments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ReceivablePayments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ReceivableCharge");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ReceivableCharge");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Prices");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Prices");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "People");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "People");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PaySchedules");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PaySchedules");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PaymentAllocation");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PaymentAllocation");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PaycheckTimeOff");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PaycheckTimeOff");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Paychecks");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Paychecks");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PaycheckOverride");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PaycheckOverride");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PaycheckLine");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PaycheckLine");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Obligation");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Obligation");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "LoyaltyTier");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "LoyaltyTier");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "LoyaltyProgress");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "LoyaltyProgress");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "LoanBalance");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "LoanBalance");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Labels");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Labels");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "InvestmentFees");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "InvestmentFees");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "InvestmentContributions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "InvestmentContributions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "IncomeSources");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "IncomeSources");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "IncomeReceipts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "IncomeReceipts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "HsaYears");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "HsaYears");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "HsaMonth");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "HsaMonth");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "HsaContribution");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "HsaContribution");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Holdings");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Holdings");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "EstimatedTaxPayments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "EstimatedTaxPayments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "EarnRules");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "EarnRules");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Dividends");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Dividends");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "DepositSplit");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "DepositSplit");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "DeductionElections");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "DeductionElections");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ContributionLimits");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ContributionLimits");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CategoryRules");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CategoryRules");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CardPerkUse");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CardPerkUse");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CardPerk");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CardPerk");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CardFee");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CardFee");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CardBalances");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CardBalances");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "BudgetPeriods");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "BudgetPeriods");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "BudgetLines");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "BudgetLines");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "BudgetLineAmount");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "BudgetLineAmount");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AssetValue");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AssetValue");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AccountBalances");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AccountBalances");
        }
    }
}
