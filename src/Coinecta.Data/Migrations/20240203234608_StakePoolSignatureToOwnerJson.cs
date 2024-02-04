using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class StakePoolSignatureToOwnerJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SignatureJson",
                schema: "coinecta",
                table: "StakePoolByAddresses",
                newName: "OwnerJson");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OwnerJson",
                schema: "coinecta",
                table: "StakePoolByAddresses",
                newName: "SignatureJson");
        }
    }
}
