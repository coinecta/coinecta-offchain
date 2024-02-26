using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class Interest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Interest_Denominator",
                schema: "coinecta",
                table: "StakePositionByStakeKeys",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Interest_Numerator",
                schema: "coinecta",
                table: "StakePositionByStakeKeys",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Interest_Denominator",
                schema: "coinecta",
                table: "StakePositionByStakeKeys");

            migrationBuilder.DropColumn(
                name: "Interest_Numerator",
                schema: "coinecta",
                table: "StakePositionByStakeKeys");
        }
    }
}
