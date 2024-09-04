using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVestingTreasuryAmountColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Amount_Coin",
                schema: "public",
                table: "VestingTreasuryBySlot",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<JsonElement>(
                name: "Amount_MultiAssetJson",
                schema: "public",
                table: "VestingTreasuryBySlot",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UtxoStatus",
                schema: "public",
                table: "VestingTreasuryBySlot",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount_Coin",
                schema: "public",
                table: "VestingTreasuryById",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<JsonElement>(
                name: "Amount_MultiAssetJson",
                schema: "public",
                table: "VestingTreasuryById",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount_Coin",
                schema: "public",
                table: "VestingTreasuryBySlot");

            migrationBuilder.DropColumn(
                name: "Amount_MultiAssetJson",
                schema: "public",
                table: "VestingTreasuryBySlot");

            migrationBuilder.DropColumn(
                name: "UtxoStatus",
                schema: "public",
                table: "VestingTreasuryBySlot");

            migrationBuilder.DropColumn(
                name: "Amount_Coin",
                schema: "public",
                table: "VestingTreasuryById");

            migrationBuilder.DropColumn(
                name: "Amount_MultiAssetJson",
                schema: "public",
                table: "VestingTreasuryById");
        }
    }
}
