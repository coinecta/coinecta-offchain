using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVestingTreasuryBySlotPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_VestingTreasuryBySlot",
                schema: "public",
                table: "VestingTreasuryBySlot");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VestingTreasuryBySlot",
                schema: "public",
                table: "VestingTreasuryBySlot",
                columns: new[] { "Slot", "TxHash", "TxIndex", "UtxoStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_VestingTreasuryBySlot",
                schema: "public",
                table: "VestingTreasuryBySlot");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VestingTreasuryBySlot",
                schema: "public",
                table: "VestingTreasuryBySlot",
                columns: new[] { "Slot", "TxHash", "TxIndex" });
        }
    }
}
