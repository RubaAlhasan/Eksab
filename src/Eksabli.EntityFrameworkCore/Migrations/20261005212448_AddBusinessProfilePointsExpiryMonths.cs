using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eksabli.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessProfilePointsExpiryMonths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PointsExpiryMonths",
                table: "AppBusinessProfiles",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointsExpiryMonths",
                table: "AppBusinessProfiles");
        }
    }
}
