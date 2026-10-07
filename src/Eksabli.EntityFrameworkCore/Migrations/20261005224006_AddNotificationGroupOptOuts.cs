using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eksabli.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationGroupOptOuts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppNotificationGroupOptOuts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Group = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppNotificationGroupOptOuts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppNotificationGroupOptOuts_UserId_Group",
                table: "AppNotificationGroupOptOuts",
                columns: new[] { "UserId", "Group" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppNotificationGroupOptOuts");
        }
    }
}
