using Microsoft.EntityFrameworkCore;
using PallasDotnet.Models;
using Coinecta.Data;
using Address = CardanoSharp.Wallet.Models.Addresses.Address;
using CardanoSharp.Wallet.Extensions.Models;
using Coinecta.Data.Models.Datums;
using Cardano.Sync.Reducers;
using Coinecta.Data.Models.Reducers;
using Cardano.Sync.Data.Models.Datums;

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
        var rollbackSlot = response.Block.Slot;
        _dbContext.StakePoolByAddresses.RemoveRange(_dbContext.StakePoolByAddresses.Where(s => s.Slot > rollbackSlot).AsNoTracking());
        _dbContext.Dispose();
        return Task.CompletedTask;
    }

    public async Task RollForwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();
        await ProcessInputAync(response);
        await ProcessOutputAync(response);
        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }

    private Task ProcessInputAync(NextResponse response)
    {
        foreach (var txBody in response.Block.TransactionBodies)
        {
            foreach (var input in txBody.Inputs)
            {
                _dbContext.StakePoolByAddresses.RemoveRange(
                    _dbContext.StakePoolByAddresses.AsNoTracking().Where(s => s.TxHash == input.Id.ToHex() && s.TxIndex == input.Index)
                );
            }
        }

        return Task.CompletedTask;
    }

    private Task ProcessOutputAync(NextResponse response)
    {
        foreach (var txBody in response.Block.TransactionBodies)
        {
            foreach (var output in txBody.Outputs)
            {
                var addressBech32 = output.Address.ToBech32();
                if (addressBech32.StartsWith("addr"))
                {
                    var address = new Address(output.Address.ToBech32());
                    var pkh = Convert.ToHexString(address.GetPublicKeyHash()).ToLowerInvariant();
                    if (pkh == configuration["CoinectaStakeValidatorHash"])
                    {
                        if (output.Datum is not null && output.Datum.Type == PallasDotnet.Models.DatumType.InlineDatum)
                        {
                            var datum = output.Datum.Data;
                            try
                            {
                                var stakePoolDatum = CborConverter.Deserialize<StakePool>(datum);
                                var entityUtxo = Utils.MapTransactionOutputEntity(txBody.Id.ToHex(), response.Block.Slot, output);
                                var stakePoolByAddress = new StakePoolByAddress
                                {
                                    Address = addressBech32,
                                    Slot = response.Block.Slot,
                                    TxHash = txBody.Id.ToHex(),
                                    TxIndex = Convert.ToUInt32(output.Index),
                                    StakePool = stakePoolDatum,
                                    Amount = entityUtxo.Amount
                                };

                                _dbContext.StakePoolByAddresses.Add(stakePoolByAddress);
                            }
                            catch
                            {
                                _logger.LogError("Error deserializing stake pool datum: {datum} for {txHash}#{txIndex}", 
                                    Convert.ToHexString(datum).ToLowerInvariant(), 
                                    txBody.Id.ToHex(), 
                                    output.Index
                                );
                            }
                        }
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
