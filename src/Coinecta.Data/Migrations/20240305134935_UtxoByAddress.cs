using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class UtxoByAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UtxosByAddress",
                schema: "coinecta",
                columns: table => new
                {
                    Address = table.Column<string>(type: "text", nullable: false),
                    TxHash = table.Column<string>(type: "text", nullable: false),
                    TxIndex = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TxOutCbor = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtxosByAddress", x => new { x.Address, x.Slot, x.TxHash, x.TxIndex, x.Status });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UtxosByAddress",
                schema: "coinecta");
        }
    }
}
