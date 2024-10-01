using Cardano.Sync.Data;
using Coinecta.Data.Models.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Coinecta.Data.Models;

public class CoinectaDbContext : CardanoDbContext
{
    public DbSet<VestingTreasuryById> VestingTreasuryById { get; set; } = default!;
    public DbSet<VestingTreasuryBySlot> VestingTreasuryBySlot { get; set; } = default!;
    public DbSet<VestingTreasurySubmittedTx> VestingTreasurySubmittedTxs { get; set; } = default!;
    public DbSet<VestingClaimEntryByRootHash> VestingClaimEntryByRootHash { get; set; } = default!;

    public CoinectaDbContext(DbContextOptions options, IConfiguration configuration)
        : base(options, configuration) { }

    override protected void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VestingTreasuryById>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Slot).IsRequired();
            entity.Property(e => e.TxHash).IsRequired();
            entity.Property(e => e.TxIndex).IsRequired();
            entity.Property(e => e.UtxoRaw).IsRequired();
            entity.Property(e => e.RootHash).IsRequired();
            entity.Ignore(e => e.TreasuryDatum);
            entity.Ignore(e => e.Utxo);
        });

        modelBuilder.Entity<VestingTreasuryBySlot>(entity =>
        {
            entity.HasKey(e => new { e.Slot, e.TxHash, e.TxIndex, e.UtxoStatus, e.Id });
            entity.Property(e => e.Slot).IsRequired();
            entity.Property(e => e.BlockHash).IsRequired();
            entity.Property(e => e.TxHash).IsRequired();
            entity.Property(e => e.TxIndex).IsRequired();
            entity.Property(e => e.RootHash).IsRequired();
            entity.Property(e => e.UtxoRaw).IsRequired();
            entity.Ignore(e => e.TreasuryDatum);
            entity.Ignore(e => e.Utxo);
        });

        modelBuilder.Entity<VestingClaimEntryByRootHash>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RootHash).IsRequired();
            entity.Property(e => e.ClaimantPkh).IsRequired();
            entity.Property(e => e.ClaimEntryRaw).IsRequired();
            entity.Ignore(e => e.ClaimEntry);
        });

        modelBuilder.Entity<VestingTreasurySubmittedTx>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.TxHash, e.TxIndex });
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.TxHash).IsRequired();
            entity.Property(e => e.TxIndex).IsRequired();
            entity.Property(e => e.OwnerPkh).IsRequired();
            entity.Property(e => e.UtxoRaw).IsRequired();
            entity.Ignore(e => e.TreasuryDatum);
            entity.Ignore(e => e.Utxo);
        });
    }
}