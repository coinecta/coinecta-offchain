using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Coinecta.Data.Models;
using Coinecta.Data.Models.Reducers;
using Cardano.Sync.Data;

namespace Coinecta.Data;

public class CoinectaDbContext
(
    DbContextOptions<CoinectaDbContext> options,
    IConfiguration configuration
) : CardanoDbContext(options, configuration)
{
    private readonly IConfiguration _configuration = configuration;
    public DbSet<StakePoolByAddress> StakePoolByAddresses { get; set; }
    public DbSet<StakeRequestByAddress> StakeRequestByAddresses { get; set; }
    public DbSet<StakePositionByStakeKey> StakePositionByStakeKeys { get; set; }
    public DbSet<UtxoByAddress> UtxosByAddress { get; set; }
    public DbSet<NftByAddress> NftsByAddress { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StakePoolByAddress>().HasKey(item => new { item.Address, item.Slot, item.TxHash, item.TxIndex, item.UtxoStatus });
        modelBuilder.Entity<StakePoolByAddress>().OwnsOne(item => item.Amount);
        modelBuilder.Entity<StakeRequestByAddress>().HasKey(item => new { item.Address, item.Slot, item.TxHash, item.TxIndex });
        modelBuilder.Entity<StakeRequestByAddress>().OwnsOne(item => item.Amount);
        modelBuilder.Entity<StakePositionByStakeKey>().HasKey(item => new { item.StakeKey, item.Slot, item.TxHash, item.TxIndex, item.UtxoStatus });
        modelBuilder.Entity<StakePositionByStakeKey>().OwnsOne(item => item.Amount);
        modelBuilder.Entity<StakePositionByStakeKey>().OwnsOne(item => item.Interest);
        modelBuilder.Entity<UtxoByAddress>().HasKey(item => new { item.Address, item.Slot, item.TxHash, item.TxIndex, item.Status });
        modelBuilder.Entity<NftByAddress>().HasKey(item => new { item.TxHash, item.OutputIndex, item.Slot, item.PolicyId, item.AssetName, item.UtxoStatus });
        base.OnModelCreating(modelBuilder);
    }
}