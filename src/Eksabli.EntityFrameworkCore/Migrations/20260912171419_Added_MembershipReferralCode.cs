using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eksabli.Migrations
{
    /// <inheritdoc />
    public partial class Added_MembershipReferralCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "AppMemberships",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppMemberships_TenantId_ReferralCode",
                table: "AppMemberships",
                columns: new[] { "TenantId", "ReferralCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppMemberships_TenantId_ReferralCode",
                table: "AppMemberships");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "AppMemberships");
        }
    }
}
