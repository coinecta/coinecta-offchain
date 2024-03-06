using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class StakePositionByStakeKeyUtxoStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StakePositionByStakeKeys",
                schema: "coinecta",
                table: "StakePositionByStakeKeys");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StakePositionByStakeKeys",
                schema: "coinecta",
                table: "StakePositionByStakeKeys",
                columns: new[] { "StakeKey", "Slot", "TxHash", "TxIndex", "UtxoStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StakePositionByStakeKeys",
                schema: "coinecta",
                table: "StakePositionByStakeKeys");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StakePositionByStakeKeys",
                schema: "coinecta",
                table: "StakePositionByStakeKeys",
                columns: new[] { "StakeKey", "Slot", "TxHash", "TxIndex" });
        }
    }
}
