using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class StakePositionByStakeKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StakePositionByStakeKeys",
                schema: "coinecta",
                columns: table => new
                {
                    StakeKey = table.Column<string>(type: "text", nullable: false),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TxHash = table.Column<string>(type: "text", nullable: false),
                    TxIndex = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Amount_Coin = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Amount_MultiAssetJson = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    StakePositionJson = table.Column<JsonElement>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StakePositionByStakeKeys", x => new { x.StakeKey, x.Slot, x.TxHash, x.TxIndex });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StakePositionByStakeKeys",
                schema: "coinecta");
        }
    }
}
