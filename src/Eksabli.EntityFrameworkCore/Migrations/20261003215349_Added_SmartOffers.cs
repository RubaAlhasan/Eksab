using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eksabli.Migrations
{
    /// <inheritdoc />
    public partial class Added_SmartOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSmartOfferInventories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SmartOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reserved = table.Column<int>(type: "integer", nullable: false),
                    Sold = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSmartOfferInventories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSmartOfferOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SmartOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    OfferTitleAr = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OfferTitleEn = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PlacedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReservationExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSmartOfferOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSmartOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TitleAr = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TitleEn = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DescriptionAr = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DescriptionEn = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Strategy = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DailyQuantity = table.Column<int>(type: "integer", nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSmartOffers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSmartOfferPriceStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmartOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartMinute = table.Column<int>(type: "integer", nullable: false),
                    EndMinute = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    QuantityLimit = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSmartOfferPriceStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppSmartOfferPriceStages_AppSmartOffers_SmartOfferId",
                        column: x => x.SmartOfferId,
                        principalTable: "AppSmartOffers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSmartOfferInventories_SlotId_ServiceDate",
                table: "AppSmartOfferInventories",
                columns: new[] { "SlotId", "ServiceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSmartOfferInventories_TenantId_SmartOfferId_ServiceDate",
                table: "AppSmartOfferInventories",
                columns: new[] { "TenantId", "SmartOfferId", "ServiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppSmartOfferOrders_Code",
                table: "AppSmartOfferOrders",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSmartOfferOrders_MembershipId_SmartOfferId_Status",
                table: "AppSmartOfferOrders",
                columns: new[] { "MembershipId", "SmartOfferId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppSmartOfferOrders_Status_ReservationExpiresAt",
                table: "AppSmartOfferOrders",
                columns: new[] { "Status", "ReservationExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppSmartOfferOrders_TenantId_Status",
                table: "AppSmartOfferOrders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppSmartOfferPriceStages_SmartOfferId",
                table: "AppSmartOfferPriceStages",
                column: "SmartOfferId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSmartOffers_TenantId_IsEnabled",
                table: "AppSmartOffers",
                columns: new[] { "TenantId", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSmartOfferInventories");

            migrationBuilder.DropTable(
                name: "AppSmartOfferOrders");

            migrationBuilder.DropTable(
                name: "AppSmartOfferPriceStages");

            migrationBuilder.DropTable(
                name: "AppSmartOffers");
        }
    }
}
