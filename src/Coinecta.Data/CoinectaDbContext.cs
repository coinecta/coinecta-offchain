using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Coinecta.Data.Models;
using Coinecta.Data.Models.Reducers;

namespace Coinecta.Data;

public class CoinectaDbContext(DbContextOptions<CoinectaDbContext> options, IConfiguration configuration) : DbContext(options)
{
    private readonly IConfiguration _configuration = configuration;
    public DbSet<Block> Blocks { get; set; }
    public DbSet<TransactionOutput> TransactionOutputs { get; set; }
    public DbSet<StakePoolByAddress> StakePoolByAddresses { get; set; }
    public DbSet<StakeRequestByAddress> StakeRequestByAddresses { get; set; }
    public DbSet<StakePositionByStakeKey> StakePositionByStakeKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_configuration.GetConnectionString("CoinectaContextSchema"));
        modelBuilder.Entity<Block>().HasKey(b => new { b.Id, b.Number, b.Slot });
        modelBuilder.Entity<TransactionOutput>().HasKey(item => new { item.Id, item.Index });
        modelBuilder.Entity<TransactionOutput>().OwnsOne(item => item.Amount);
        modelBuilder.Entity<TransactionOutput>().OwnsOne(item => item.Datum);
        modelBuilder.Entity<StakePoolByAddress>().HasKey(item => new { item.Address, item.Slot, item.TxHash, item.TxIndex });
        modelBuilder.Entity<StakePoolByAddress>().OwnsOne(item => item.Amount);
        modelBuilder.Entity<StakeRequestByAddress>().HasKey(item => new { item.Address, item.Slot, item.TxHash, item.TxIndex });
        modelBuilder.Entity<StakeRequestByAddress>().OwnsOne(item => item.Amount);
        modelBuilder.Entity<StakePositionByStakeKey>().HasKey(item => new { item.StakeKey, item.Slot, item.TxHash, item.TxIndex });
        modelBuilder.Entity<StakePositionByStakeKey>().OwnsOne(item => item.Amount);
        modelBuilder.Entity<StakePositionByStakeKey>().OwnsOne(item => item.Interest);
        base.OnModelCreating(modelBuilder);
    }
}