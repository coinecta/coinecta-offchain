using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class UtxoByAddressTrackerLastRequestedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UtxosByAddress_LastRequested",
                schema: "coinecta",
                table: "UtxosByAddress",
                column: "LastRequested");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UtxosByAddress_LastRequested",
                schema: "coinecta",
                table: "UtxosByAddress");
        }
    }
}
