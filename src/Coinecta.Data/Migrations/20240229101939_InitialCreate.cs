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
                name: "ReducerStates",
                schema: "coinecta",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReducerStates", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "StakePoolByAddresses",
                schema: "coinecta",
                columns: table => new
                {
                    Address = table.Column<string>(type: "text", nullable: false),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TxHash = table.Column<string>(type: "text", nullable: false),
                    TxIndex = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Amount_Coin = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Amount_MultiAssetJson = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    StakePoolJson = table.Column<JsonElement>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StakePoolByAddresses", x => new { x.Address, x.Slot, x.TxHash, x.TxIndex });
                });

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
                    LockTime = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Interest_Numerator = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Interest_Denominator = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StakePositionJson = table.Column<JsonElement>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StakePositionByStakeKeys", x => new { x.StakeKey, x.Slot, x.TxHash, x.TxIndex });
                });

            migrationBuilder.CreateTable(
                name: "StakeRequestByAddresses",
                schema: "coinecta",
                columns: table => new
                {
                    Address = table.Column<string>(type: "text", nullable: false),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TxHash = table.Column<string>(type: "text", nullable: false),
                    TxIndex = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Amount_Coin = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Amount_MultiAssetJson = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StakePoolJson = table.Column<JsonElement>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StakeRequestByAddresses", x => new { x.Address, x.Slot, x.TxHash, x.TxIndex });
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

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_Slot",
                schema: "coinecta",
                table: "Blocks",
                column: "Slot");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionOutputs_Slot",
                schema: "coinecta",
                table: "TransactionOutputs",
                column: "Slot");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Blocks",
                schema: "coinecta");

            migrationBuilder.DropTable(
                name: "ReducerStates",
                schema: "coinecta");

            migrationBuilder.DropTable(
                name: "StakePoolByAddresses",
                schema: "coinecta");

            migrationBuilder.DropTable(
                name: "StakePositionByStakeKeys",
                schema: "coinecta");

            migrationBuilder.DropTable(
                name: "StakeRequestByAddresses",
                schema: "coinecta");

            migrationBuilder.DropTable(
                name: "TransactionOutputs",
                schema: "coinecta");
        }
    }
}
