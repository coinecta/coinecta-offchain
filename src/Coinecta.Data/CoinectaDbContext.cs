using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Coinecta.Data.Models;

namespace Coinecta.Data;

public class CoinectaDbContext(DbContextOptions<CoinectaDbContext> options, IConfiguration configuration) : DbContext(options)
{
    private readonly IConfiguration _configuration = configuration;
    public DbSet<Block> Blocks { get; set; }
    public DbSet<TransactionOutput> TransactionOutputs { get; set; }
    public DbSet<TbcByAddress> TbcByAddress { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_configuration.GetConnectionString("CoinectaContextSchema"));
        modelBuilder.Entity<Block>().HasKey(b => new { b.Id, b.Number, b.Slot });
        modelBuilder.Entity<TransactionOutput>().HasKey(item => new { item.Id, item.Index });
        modelBuilder.Entity<TransactionOutput>().OwnsOne(item => item.Amount);
        modelBuilder.Entity<TbcByAddress>().HasKey(item => new { item.Address, item.Slot });
        modelBuilder.Entity<TbcByAddress>().OwnsOne(item => item.Amount);
        base.OnModelCreating(modelBuilder);
    }
}