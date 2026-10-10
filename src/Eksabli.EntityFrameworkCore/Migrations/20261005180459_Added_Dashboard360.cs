using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eksabli.Migrations
{
    /// <inheritdoc />
    public partial class Added_Dashboard360 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "AppBusinessProfiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Asia/Damascus");

            migrationBuilder.CreateIndex(
                name: "IX_AppSmartOfferOrders_Status_CompletedAt",
                table: "AppSmartOfferOrders",
                columns: new[] { "Status", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppSmartOfferOrders_TenantId_PlacedAt",
                table: "AppSmartOfferOrders",
                columns: new[] { "TenantId", "PlacedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPointsTransactions_Type_CreationTime",
                table: "AppPointsTransactions",
                columns: new[] { "Type", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppSmartOfferOrders_Status_CompletedAt",
                table: "AppSmartOfferOrders");

            migrationBuilder.DropIndex(
                name: "IX_AppSmartOfferOrders_TenantId_PlacedAt",
                table: "AppSmartOfferOrders");

            migrationBuilder.DropIndex(
                name: "IX_AppPointsTransactions_Type_CreationTime",
                table: "AppPointsTransactions");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "AppBusinessProfiles");
        }
    }
}
