﻿// <auto-generated />
using System.Text.Json;
using Coinecta.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Coinecta.Data.Migrations
{
    [DbContext(typeof(CoinectaDbContext))]
    [Migration("20240906024921_RemoveNftId")]
    partial class RemoveNftId
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("public")
                .HasAnnotation("ProductVersion", "8.0.8")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Cardano.Sync.Data.Models.Block", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<decimal>("Number")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("Slot")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("Id", "Number", "Slot");

                    b.HasIndex("Slot");

                    b.ToTable("Blocks", "public");
                });

            modelBuilder.Entity("Cardano.Sync.Data.Models.ReducerState", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.Property<string>("Hash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<decimal>("Slot")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("Name");

                    b.ToTable("ReducerStates", "public");
                });

            modelBuilder.Entity("Cardano.Sync.Data.Models.TransactionOutput", b =>
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

                    b.HasIndex("Slot");

                    b.ToTable("TransactionOutputs", "public");
                });

            modelBuilder.Entity("Coinecta.Data.Models.Entity.VestingClaimEntryByRootHash", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<byte[]>("ClaimEntryRaw")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.Property<string>("ClaimantPkh")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("RootHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("VestingClaimEntryByRootHash", "public");
                });

            modelBuilder.Entity("Coinecta.Data.Models.Entity.VestingTreasuryById", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<string>("OwnerPkh")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("Slot")
                        .HasColumnType("bigint");

                    b.Property<string>("TxHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("TxIndex")
                        .HasColumnType("bigint");

                    b.Property<byte[]>("UtxoRaw")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.HasKey("Id");

                    b.ToTable("VestingTreasuryById", "public");
                });

            modelBuilder.Entity("Coinecta.Data.Models.Entity.VestingTreasuryBySlot", b =>
                {
                    b.Property<long>("Slot")
                        .HasColumnType("bigint");

                    b.Property<string>("TxHash")
                        .HasColumnType("text");

                    b.Property<long>("TxIndex")
                        .HasColumnType("bigint");

                    b.Property<int>("UtxoStatus")
                        .HasColumnType("integer");

                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<string>("BlockHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("OwnerPkh")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Type")
                        .HasColumnType("integer");

                    b.Property<byte[]>("UtxoRaw")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.HasKey("Slot", "TxHash", "TxIndex", "UtxoStatus", "Id");

                    b.ToTable("VestingTreasuryBySlot", "public");
                });

            modelBuilder.Entity("Cardano.Sync.Data.Models.TransactionOutput", b =>
                {
                    b.OwnsOne("Cardano.Sync.Data.Models.Datum", "Datum", b1 =>
                        {
                            b1.Property<string>("TransactionOutputId")
                                .HasColumnType("text");

                            b1.Property<long>("TransactionOutputIndex")
                                .HasColumnType("bigint");

                            b1.Property<byte[]>("Data")
                                .IsRequired()
                                .HasColumnType("bytea");

                            b1.Property<int>("Type")
                                .HasColumnType("integer");

                            b1.HasKey("TransactionOutputId", "TransactionOutputIndex");

                            b1.ToTable("TransactionOutputs", "public");

                            b1.WithOwner()
                                .HasForeignKey("TransactionOutputId", "TransactionOutputIndex");
                        });

                    b.OwnsOne("Cardano.Sync.Data.Models.Value", "Amount", b1 =>
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

                            b1.ToTable("TransactionOutputs", "public");

                            b1.WithOwner()
                                .HasForeignKey("TransactionOutputId", "TransactionOutputIndex");
                        });

                    b.Navigation("Amount")
                        .IsRequired();

                    b.Navigation("Datum");
                });
#pragma warning restore 612, 618
        }
    }
}
