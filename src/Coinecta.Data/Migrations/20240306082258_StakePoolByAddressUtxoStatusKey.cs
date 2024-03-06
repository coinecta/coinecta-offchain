using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class StakePoolByAddressUtxoStatusKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StakePoolByAddresses",
                schema: "coinecta",
                table: "StakePoolByAddresses");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StakePoolByAddresses",
                schema: "coinecta",
                table: "StakePoolByAddresses",
                columns: new[] { "Address", "Slot", "TxHash", "TxIndex", "UtxoStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StakePoolByAddresses",
                schema: "coinecta",
                table: "StakePoolByAddresses");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StakePoolByAddresses",
                schema: "coinecta",
                table: "StakePoolByAddresses",
                columns: new[] { "Address", "Slot", "TxHash", "TxIndex" });
        }
    }
}
