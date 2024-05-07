using Coinecta.Data.Utils;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateGetTransactionHistoryByAddressFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET search_path TO coinecta");

            var sql = ResourceUtils.GetEmbeddedResourceSql("CreateGetTransactionHistoryByAddress.sql");

            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET search_path TO coinecta");

            var sql = ResourceUtils.GetEmbeddedResourceSql("DropGetTransactionHistoryByAddress.sql");

            migrationBuilder.Sql(sql);
        }
    }
}
