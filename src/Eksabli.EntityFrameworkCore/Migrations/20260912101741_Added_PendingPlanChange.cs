using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eksabli.Migrations
{
    /// <inheritdoc />
    public partial class Added_PendingPlanChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PendingPlanId",
                table: "AppTenantSubscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanChangeRequestedAt",
                table: "AppTenantSubscriptions",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingPlanId",
                table: "AppTenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "PlanChangeRequestedAt",
                table: "AppTenantSubscriptions");
        }
    }
}
