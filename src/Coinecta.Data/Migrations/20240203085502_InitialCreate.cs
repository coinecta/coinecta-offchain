using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "coinecta");

            migrationBuilder.CreateTable(
                name: "Blocks",
                schema: "coinecta",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Number = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blocks", x => new { x.Id, x.Number, x.Slot });
                });

            migrationBuilder.CreateTable(
                name: "StakePoolByAddresses",
                schema: "coinecta",
                columns: table => new
                {
                    Address = table.Column<string>(type: "text", nullable: false),
                    TxHash = table.Column<string>(type: "text", nullable: false),
                    TxIndex = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SignatureJson = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    RewardSettingsJson = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    PolicyId = table.Column<string>(type: "text", nullable: false),
                    AssetName = table.Column<string>(type: "text", nullable: false),
                    Decimals = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Amount_Coin = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Amount_MultiAssetJson = table.Column<JsonElement>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StakePoolByAddresses", x => new { x.Address, x.Slot, x.TxHash, x.TxIndex });
                });

            migrationBuilder.CreateTable(
                name: "TransactionOutputs",
                schema: "coinecta",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Index = table.Column<long>(type: "bigint", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    Amount_Coin = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Amount_MultiAssetJson = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    Datum_Type = table.Column<int>(type: "integer", nullable: true),
                    Datum_Data = table.Column<byte[]>(type: "bytea", nullable: true),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionOutputs", x => new { x.Id, x.Index });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Blocks",
                schema: "coinecta");

            migrationBuilder.DropTable(
                name: "StakePoolByAddresses",
                schema: "coinecta");

            migrationBuilder.DropTable(
                name: "TransactionOutputs",
                schema: "coinecta");
        }
    }
}
