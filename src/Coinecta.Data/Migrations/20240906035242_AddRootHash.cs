using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRootHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RootHash",
                schema: "public",
                table: "VestingTreasuryBySlot",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RootHash",
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
                name: "RootHash",
                schema: "public",
                table: "VestingTreasuryBySlot");

            migrationBuilder.DropColumn(
                name: "RootHash",
                schema: "public",
                table: "VestingTreasuryById");
        }
    }
}
