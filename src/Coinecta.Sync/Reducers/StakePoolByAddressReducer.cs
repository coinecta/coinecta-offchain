using Microsoft.EntityFrameworkCore;
using PallasDotnet.Models;
using Coinecta.Data;
using Address = CardanoSharp.Wallet.Models.Addresses.Address;
using CardanoSharp.Wallet.Extensions.Models;
using Coinecta.Data.Models.Datums;
using Cardano.Sync.Reducers;
using Coinecta.Data.Models.Reducers;
using Cardano.Sync.Data.Models.Datums;
using CardanoSharp.Wallet.Models;
using Coinecta.Data.Models.Enums;

namespace Coinecta.Sync.Reducers;

public class StakePoolByAddressReducer(
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    ILogger<StakePoolByAddressReducer> logger
) : IReducer
{
    private CoinectaDbContext _dbContext = default!;
    private readonly ILogger<StakePoolByAddressReducer> _logger = logger;

    public Task RollBackwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();
        ulong rollbackSlot = response.Block.Slot;
        _dbContext.StakePoolByAddresses.RemoveRange(_dbContext.StakePoolByAddresses.Where(s => s.Slot > rollbackSlot));
        _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
        return Task.CompletedTask;
    }

    public async Task RollForwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();

        foreach (TransactionBody txBody in response.Block.TransactionBodies)
        {
            await ProcessTransactionBodyAsync(response.Block, txBody);
        }

        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }

    private async Task ProcessTransactionBodyAsync(Block block, TransactionBody tx)
    {
        await ProcessInputAync(block, tx);
        await ProcessOutputAync(block, tx);
    }

    private async Task ProcessInputAync(Block block, TransactionBody tx)
    {
        foreach (TransactionInput input in tx.Inputs)
        {
            // First check in-memory data
            StakePoolByAddress? stakePool = _dbContext.StakePoolByAddresses.Local
                .Where(s => s.TxHash == input.Id.ToHex())
                .Where(s => s.TxIndex == input.Index)
                .FirstOrDefault();

            // Then check the database
            stakePool ??= await _dbContext.StakePoolByAddresses
                .AsNoTracking()
                .Where(s => s.TxHash == input.Id.ToHex())
                .Where(s => s.TxIndex == input.Index)
                .FirstOrDefaultAsync();

            if (stakePool is not null)
            {
                StakePoolByAddress stakePoolByAddress = new()
                {
                    Address = stakePool.Address,
                    Slot = block.Slot,
                    TxHash = stakePool.TxHash,
                    TxIndex = stakePool.TxIndex,
                    StakePool = stakePool.StakePool,
                    Amount = new()
                    {
                        Coin = stakePool.Amount.Coin,
                        MultiAsset = stakePool.Amount.MultiAsset
                    },
                    UtxoStatus = UtxoStatus.Spent
                };

                _dbContext.StakePoolByAddresses.Add(stakePoolByAddress);
            }
        }
    }

    private Task ProcessOutputAync(Block block, TransactionBody tx)
    {
        tx.Outputs.ToList().ForEach(output =>
        {
            string addressBech32 = output.Address.ToBech32();
            if (addressBech32.StartsWith("addr"))
            {
                Address address = new(output.Address.ToBech32());
                string pkh = Convert.ToHexString(address.GetPublicKeyHash()).ToLowerInvariant();
                if (pkh == configuration["CoinectaStakeValidatorHash"])
                {
                    if (output.Datum is not null && output.Datum.Type == PallasDotnet.Models.DatumType.InlineDatum)
                    {
                        byte[] datum = output.Datum.Data;
                        try
                        {
                            StakePool stakePoolDatum = CborConverter.Deserialize<StakePool>(datum);
                            Cardano.Sync.Data.Models.TransactionOutput entityUtxo = Utils.MapTransactionOutputEntity(tx.Id.ToHex(), block.Slot, output);
                            StakePoolByAddress stakePoolByAddress = new()
                            {
                                Address = addressBech32,
                                Slot = block.Slot,
                                TxHash = tx.Id.ToHex(),
                                TxIndex = Convert.ToUInt32(output.Index),
                                StakePool = stakePoolDatum,
                                Amount = entityUtxo.Amount,
                                UtxoStatus = UtxoStatus.Unspent
                            };

                            _dbContext.StakePoolByAddresses.Add(stakePoolByAddress);
                        }
                        catch
                        {
                            _logger.LogError("Error deserializing stake pool datum: {datum} for {txHash}#{txIndex}",
                                Convert.ToHexString(datum).ToLowerInvariant(),
                                tx.Id.ToHex(),
                                output.Index
                            );
                        }
                    }
                }
            }
        });
        return Task.CompletedTask;
    }
}
