using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNftId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NftId",
                schema: "public",
                table: "VestingClaimEntryByRootHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NftId",
                schema: "public",
                table: "VestingClaimEntryByRootHash",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
