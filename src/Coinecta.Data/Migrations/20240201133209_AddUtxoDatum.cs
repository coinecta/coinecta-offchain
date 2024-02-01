using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUtxoDatum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Datum_Data",
                schema: "coinecta",
                table: "TransactionOutputs",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Datum_Type",
                schema: "coinecta",
                table: "TransactionOutputs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Datum_Data",
                schema: "coinecta",
                table: "TransactionOutputs");

            migrationBuilder.DropColumn(
                name: "Datum_Type",
                schema: "coinecta",
                table: "TransactionOutputs");
        }
    }
}
