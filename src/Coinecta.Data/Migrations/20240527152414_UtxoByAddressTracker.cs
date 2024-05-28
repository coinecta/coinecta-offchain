using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class UtxoByAddressTracker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the existing table
            migrationBuilder.DropTable(
                name: "UtxosByAddress",
                schema: "coinecta");

            // Create the new table with updated schema
            migrationBuilder.CreateTable(
                name: "UtxosByAddress",
                schema: "coinecta",
                columns: table => new
                {
                    Address = table.Column<string>(nullable: false),
                    LastUpdated = table.Column<DateTime>(nullable: false),
                    LastRequested = table.Column<DateTime>(nullable: false),
                    UtxoListCborBytes = table.Column<byte[]>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtxosByAddress", x => x.Address);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the new table
            migrationBuilder.DropTable(
                name: "UtxosByAddress",
                schema: "coinecta");

            // Recreate the old table with its original schema
            migrationBuilder.CreateTable(
                name: "UtxosByAddress",
                schema: "coinecta",
                columns: table => new
                {
                    Address = table.Column<string>(nullable: false),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TxHash = table.Column<string>(type: "text", nullable: false),
                    TxIndex = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TxOutCbor = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtxosByAddress", x => new { x.Address, x.Slot, x.TxHash, x.TxIndex, x.Status });
                });
        }
    }
}
