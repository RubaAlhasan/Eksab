using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eksabli.Migrations
{
    /// <inheritdoc />
    public partial class Added_Currency_To_Wallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppPointRules_TenantId_RuleType",
                table: "AppPointRules");

            // EF's migration scaffolder cannot know which of the two new columns the old single
            // MonthlyPrice column "becomes" — it picked MonthlyPriceUsd here, which would silently
            // relabel every existing plan's price as USD and zero out its real (implicitly SYP) price.
            // This repo's current prices are all implicitly SYP (the platform's default/local
            // currency), so the rename target must be MonthlyPriceSyp instead — corrected by hand.
            migrationBuilder.RenameColumn(
                name: "MonthlyPrice",
                table: "AppSubscriptionPlans",
                newName: "MonthlyPriceSyp");

            migrationBuilder.AddColumn<int>(
                name: "Currency",
                table: "AppTenantSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyPriceUsd",
                table: "AppSubscriptionPlans",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "AppPointsTransactions",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Currency",
                table: "AppPointsTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Currency",
                table: "AppPointRules",
                type: "integer",
                nullable: true);

            // PointRule.Currency is semantically REQUIRED for RuleType=PerCurrencyUnit (0) — unlike
            // PerVisit, where it's legitimately always null. Any pre-existing PerCurrencyUnit row would
            // otherwise sit at Currency=NULL and silently stop matching PosAppService's now
            // currency-scoped rule lookup. Backfilled to Syp (0), this platform's implicit currency
            // until now. PerVisit rows are untouched (stay NULL, which is correct for them).
            migrationBuilder.Sql(
                "UPDATE \"AppPointRules\" SET \"Currency\" = 0 WHERE \"RuleType\" = 0 AND \"Currency\" IS NULL;");

            migrationBuilder.AddColumn<int>(
                name: "Currency",
                table: "AppInvoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AppPointRules_TenantId_RuleType_Currency",
                table: "AppPointRules",
                columns: new[] { "TenantId", "RuleType", "Currency" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppPointRules_TenantId_RuleType_Currency",
                table: "AppPointRules");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "AppTenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "MonthlyPriceUsd",
                table: "AppSubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "AppPointsTransactions");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "AppPointsTransactions");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "AppPointRules");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "AppInvoices");

            migrationBuilder.RenameColumn(
                name: "MonthlyPriceSyp",
                table: "AppSubscriptionPlans",
                newName: "MonthlyPrice");

            migrationBuilder.CreateIndex(
                name: "IX_AppPointRules_TenantId_RuleType",
                table: "AppPointRules",
                columns: new[] { "TenantId", "RuleType" },
                unique: true);
        }
    }
}
