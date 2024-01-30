﻿// <auto-generated />
using System.Text.Json;
using Coinecta.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Coinecta.Data.Migrations
{
    [DbContext(typeof(CoinectaDbContext))]
    [Migration("20240130175103_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("coinecta")
                .HasAnnotation("ProductVersion", "8.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Coinecta.Data.Models.Block", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<decimal>("Number")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("Slot")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("Id", "Number", "Slot");

                    b.ToTable("Blocks", "coinecta");
                });

            modelBuilder.Entity("Coinecta.Data.Models.TbcByAddress", b =>
                {
                    b.Property<string>("Address")
                        .HasColumnType("text");

                    b.Property<decimal>("Slot")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("Address", "Slot");

                    b.ToTable("TbcByAddress", "coinecta");
                });

            modelBuilder.Entity("Coinecta.Data.Models.TransactionOutput", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<long>("Index")
                        .HasColumnType("bigint");

                    b.Property<string>("Address")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<decimal>("Slot")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("Id", "Index");

                    b.ToTable("TransactionOutputs", "coinecta");
                });

            modelBuilder.Entity("Coinecta.Data.Models.TbcByAddress", b =>
                {
                    b.OwnsOne("Coinecta.Data.Models.Value", "Amount", b1 =>
                        {
                            b1.Property<string>("TbcByAddressAddress")
                                .HasColumnType("text");

                            b1.Property<decimal>("TbcByAddressSlot")
                                .HasColumnType("numeric(20,0)");

                            b1.Property<decimal>("Coin")
                                .HasColumnType("numeric(20,0)");

                            b1.Property<JsonElement>("MultiAssetJson")
                                .HasColumnType("jsonb");

                            b1.HasKey("TbcByAddressAddress", "TbcByAddressSlot");

                            b1.ToTable("TbcByAddress", "coinecta");

                            b1.WithOwner()
                                .HasForeignKey("TbcByAddressAddress", "TbcByAddressSlot");
                        });

                    b.Navigation("Amount")
                        .IsRequired();
                });

            modelBuilder.Entity("Coinecta.Data.Models.TransactionOutput", b =>
                {
                    b.OwnsOne("Coinecta.Data.Models.Value", "Amount", b1 =>
                        {
                            b1.Property<string>("TransactionOutputId")
                                .HasColumnType("text");

                            b1.Property<long>("TransactionOutputIndex")
                                .HasColumnType("bigint");

                            b1.Property<decimal>("Coin")
                                .HasColumnType("numeric(20,0)");

                            b1.Property<JsonElement>("MultiAssetJson")
                                .HasColumnType("jsonb");

                            b1.HasKey("TransactionOutputId", "TransactionOutputIndex");

                            b1.ToTable("TransactionOutputs", "coinecta");

                            b1.WithOwner()
                                .HasForeignKey("TransactionOutputId", "TransactionOutputIndex");
                        });

                    b.Navigation("Amount")
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
