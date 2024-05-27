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
            migrationBuilder.DropPrimaryKey(
                name: "PK_UtxosByAddress",
                schema: "coinecta",
                table: "UtxosByAddress");

            migrationBuilder.DropColumn(
                name: "Slot",
                schema: "coinecta",
                table: "UtxosByAddress");

            migrationBuilder.DropColumn(
                name: "TxHash",
                schema: "coinecta",
                table: "UtxosByAddress");

            migrationBuilder.DropColumn(
                name: "TxIndex",
                schema: "coinecta",
                table: "UtxosByAddress");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "coinecta",
                table: "UtxosByAddress");

            migrationBuilder.DropColumn(
                name: "TxOutCbor",
                schema: "coinecta",
                table: "UtxosByAddress");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRequested",
                schema: "coinecta",
                table: "UtxosByAddress",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdated",
                schema: "coinecta",
                table: "UtxosByAddress",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<byte[]>(
                name: "UtxoListCborBytes",
                schema: "coinecta",
                table: "UtxosByAddress",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UtxosByAddress",
                schema: "coinecta",
                table: "UtxosByAddress",
                column: "Address");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UtxosByAddress",
                schema: "coinecta",
                table: "UtxosByAddress");

            migrationBuilder.DropColumn(
                name: "LastRequested",
                schema: "coinecta",
                table: "UtxosByAddress");

            migrationBuilder.DropColumn(
                name: "LastUpdated",
                schema: "coinecta",
                table: "UtxosByAddress");

            migrationBuilder.DropColumn(
                name: "UtxoListCborBytes",
                schema: "coinecta",
                table: "UtxosByAddress");

            migrationBuilder.AddColumn<decimal>(
                name: "Slot",
                schema: "coinecta",
                table: "UtxosByAddress",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TxHash",
                schema: "coinecta",
                table: "UtxosByAddress",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TxIndex",
                schema: "coinecta",
                table: "UtxosByAddress",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "coinecta",
                table: "UtxosByAddress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "TxOutCbor",
                schema: "coinecta",
                table: "UtxosByAddress",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UtxosByAddress",
                schema: "coinecta",
                table: "UtxosByAddress",
                columns: new[] { "Address", "Slot", "TxHash", "TxIndex", "Status" });
        }
    }
}
