using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTreasuryBySlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VestingTreasuryBySlot",
                schema: "public",
                columns: table => new
                {
                    Slot = table.Column<long>(type: "bigint", nullable: false),
                    TxHash = table.Column<string>(type: "text", nullable: false),
                    TxIndex = table.Column<long>(type: "bigint", nullable: false),
                    BlockHash = table.Column<string>(type: "text", nullable: false),
                    Datum = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VestingTreasuryBySlot", x => new { x.Slot, x.TxHash, x.TxIndex });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VestingTreasuryBySlot",
                schema: "public");
        }
    }
}
