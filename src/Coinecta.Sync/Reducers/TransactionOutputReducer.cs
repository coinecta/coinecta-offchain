using Microsoft.EntityFrameworkCore;
using PallasDotnet.Models;
using Coinecta.Data;
using TransactionOutputEntity = Coinecta.Data.Models.TransactionOutput;
using ValueEntity = Coinecta.Data.Models.Value;

namespace Coinecta.Sync.Reducers;

public class TransactionOutputReducer(
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    ILogger<TransactionOutputReducer> logger
) : ICoreReducer
{
    private CoinectaDbContext _dbContext = default!;
    private readonly ILogger<TransactionOutputReducer> _logger = logger;

    public async Task RollForwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();
        response.Block.TransactionBodies.ToList().ForEach(txBody =>
        {
            txBody.Outputs.ToList().ForEach(output =>
            {
                _dbContext.TransactionOutputs.Add(MapTransactionOutput(txBody.Id.ToHex(), response.Block.Slot, output));
            });
        });

        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }

    public async Task RollBackwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();
        var rollbackSlot = response.Block.Slot;
        var schema = configuration.GetConnectionString("CoinectaContextSchema");
        await _dbContext.Database.ExecuteSqlRawAsync($"DELETE FROM \"{schema}\".\"TransactionOutputs\" WHERE \"Slot\" > {rollbackSlot}");
        _dbContext.Dispose();
    }

    public static TransactionOutputEntity MapTransactionOutput(string TransactionId, ulong slot, TransactionOutput output)
    {
        return new TransactionOutputEntity
        {
            Id = TransactionId,
            Address = output.Address.ToBech32(),
            Slot = slot,
            Index = Convert.ToUInt32(output.Index),
            Amount = new ValueEntity
            {
                Coin = output.Amount.Coin,
                MultiAsset = output.Amount.MultiAsset.ToDictionary(
                    k => k.Key.ToHex(),
                    v => v.Value.ToDictionary(
                        k => k.Key.ToHex(),
                        v => v.Value
                    )
                )
            }
        };
    }
}