using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eksabli.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartOfferWatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSmartOfferWatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SmartOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSmartOfferWatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSmartOfferWatches_TenantId_CustomerId_SmartOfferId",
                table: "AppSmartOfferWatches",
                columns: new[] { "TenantId", "CustomerId", "SmartOfferId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSmartOfferWatches_TenantId_SmartOfferId",
                table: "AppSmartOfferWatches",
                columns: new[] { "TenantId", "SmartOfferId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSmartOfferWatches");
        }
    }
}
