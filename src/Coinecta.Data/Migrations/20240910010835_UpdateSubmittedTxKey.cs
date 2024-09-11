using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSubmittedTxKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_VestingTreasurySubmittedTxs",
                schema: "public",
                table: "VestingTreasurySubmittedTxs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VestingTreasurySubmittedTxs",
                schema: "public",
                table: "VestingTreasurySubmittedTxs",
                columns: new[] { "Id", "TxHash", "TxIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_VestingTreasurySubmittedTxs",
                schema: "public",
                table: "VestingTreasurySubmittedTxs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VestingTreasurySubmittedTxs",
                schema: "public",
                table: "VestingTreasurySubmittedTxs",
                column: "Id");
        }
    }
}
