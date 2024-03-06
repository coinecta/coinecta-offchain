using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class StakePoolByAddressUtxoStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UtxoStatus",
                schema: "coinecta",
                table: "StakePositionByStakeKeys",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UtxoStatus",
                schema: "coinecta",
                table: "StakePoolByAddresses",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UtxoStatus",
                schema: "coinecta",
                table: "StakePositionByStakeKeys");

            migrationBuilder.DropColumn(
                name: "UtxoStatus",
                schema: "coinecta",
                table: "StakePoolByAddresses");
        }
    }
}
