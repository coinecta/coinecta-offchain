using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVestingClaimEntryByRootHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VestingClaimEntryByRootHash",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    NftId = table.Column<string>(type: "text", nullable: false),
                    RootHash = table.Column<string>(type: "text", nullable: false),
                    ClaimantPkh = table.Column<string>(type: "text", nullable: false),
                    ClaimEntryRaw = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VestingClaimEntryByRootHash", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VestingClaimEntryByRootHash",
                schema: "public");
        }
    }
}
