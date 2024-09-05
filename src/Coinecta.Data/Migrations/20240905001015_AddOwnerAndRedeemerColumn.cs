using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerAndRedeemerColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerPkh",
                schema: "public",
                table: "VestingTreasuryBySlot",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "public",
                table: "VestingTreasuryBySlot",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OwnerPkh",
                schema: "public",
                table: "VestingTreasuryById",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerPkh",
                schema: "public",
                table: "VestingTreasuryBySlot");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "public",
                table: "VestingTreasuryBySlot");

            migrationBuilder.DropColumn(
                name: "OwnerPkh",
                schema: "public",
                table: "VestingTreasuryById");
        }
    }
}
