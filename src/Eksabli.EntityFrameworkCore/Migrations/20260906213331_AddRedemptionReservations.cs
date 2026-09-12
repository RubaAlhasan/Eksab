using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eksabli.Migrations
{
    /// <inheritdoc />
    public partial class AddRedemptionReservations : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Backfill is intentionally left at the column defaults. Coupons that already exist are in
        /// the legacy <c>Issued</c> state, whose points were debited when they were created, so
        /// <c>PointsCost = 0</c> and a null <c>ReservationExpiresAt</c> are the honest values: there is
        /// no reservation outstanding against them and nothing for the release paths to give back.
        /// PosAppService.ConfirmRedemptionAsync keys off that (<c>Coupon.HoldsReservation</c>) to hand
        /// such a coupon over without charging the customer a second time.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Reserved",
                table: "AppPointsWallets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsCost",
                table: "AppCoupons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "AppCoupons",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReservationExpiresAt",
                table: "AppCoupons",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCoupons_Status_ReservationExpiresAt",
                table: "AppCoupons",
                columns: new[] { "Status", "ReservationExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppCoupons_Status_ReservationExpiresAt",
                table: "AppCoupons");

            migrationBuilder.DropColumn(
                name: "Reserved",
                table: "AppPointsWallets");

            migrationBuilder.DropColumn(
                name: "PointsCost",
                table: "AppCoupons");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "AppCoupons");

            migrationBuilder.DropColumn(
                name: "ReservationExpiresAt",
                table: "AppCoupons");
        }
    }
}
