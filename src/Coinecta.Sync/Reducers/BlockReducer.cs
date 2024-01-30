using Microsoft.EntityFrameworkCore;
using PallasDotnet.Models;
using Coinecta.Data;
using BlockEntity = Coinecta.Data.Models.Block;
namespace Coinecta.Sync.Reducers;

public class BlockReducer(IDbContextFactory<CoinectaDbContext> dbContextFactory, ILogger<BlockReducer> logger) : IBlockReducer
{
    private CoinectaDbContext _dbContext = default!;
    private readonly ILogger<BlockReducer> _logger = logger;

    public async Task RollBackwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();
        _dbContext.Blocks.RemoveRange(_dbContext.Blocks.AsNoTracking().Where(b => b.Slot > response.Block.Slot));
        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }

    public async Task RollForwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();
        _dbContext.Blocks.Add(new BlockEntity(
            response.Block.Hash.ToHex(),
            response.Block.Number,
            response.Block.Slot
        ));

        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }
}