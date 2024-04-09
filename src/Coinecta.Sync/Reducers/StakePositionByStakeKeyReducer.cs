using Microsoft.EntityFrameworkCore;
using PallasDotnet.Models;
using Coinecta.Data;
using Address = CardanoSharp.Wallet.Models.Addresses.Address;
using CardanoSharp.Wallet.Extensions.Models;
using Coinecta.Data.Models.Datums;
using Coinecta.Data.Models.Reducers;
using Cardano.Sync.Data.Models.Datums;
using Cardano.Sync.Reducers;
using Coinecta.Data.Models.Enums;
using Cardano.Sync.Data.Models;
using TransactionOutput = PallasDotnet.Models.TransactionOutput;

namespace Coinecta.Sync.Reducers;

public class StakePositionByStakeKeyReducer(
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    ILogger<StakePositionByStakeKeyReducer> logger
) : IReducer
{
    private CoinectaDbContext _dbContext = default!;
    private readonly ILogger<StakePositionByStakeKeyReducer> _logger = logger;

    public async Task RollBackwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();
        ulong rollbackSlot = response.Block.Slot;
        _dbContext.StakePositionByStakeKeys.RemoveRange(_dbContext.StakePositionByStakeKeys.Where(s => s.Slot > rollbackSlot));
        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }

    public async Task RollForwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();

        foreach (TransactionBody txBody in response.Block.TransactionBodies)
        {
            await ProcessInputAync(response.Block, txBody);
            await ProcessOutputAync(response.Block, txBody);
        }

        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }


    private async Task ProcessInputAync(PallasDotnet.Models.Block block, TransactionBody tx)
    {
        // Collect input id and index as tuples
        foreach (TransactionInput input in tx.Inputs)
        {
            // First check in-memory data
            StakePositionByStakeKey? stakePosition = _dbContext.StakePositionByStakeKeys.Local
                .Where(s => s.TxHash == input.Id.ToHex())
                .Where(s => s.TxIndex == input.Index)
                .FirstOrDefault();

            // Then check the database
            stakePosition ??= await _dbContext.StakePositionByStakeKeys
                .AsNoTracking()
                .Where(s => s.TxHash == input.Id.ToHex())
                .Where(s => s.TxIndex == input.Index)
                .FirstOrDefaultAsync();

            if (stakePosition is not null)
            {
                StakePositionByStakeKey stakePositionByKey = new()
                {
                    StakeKey = stakePosition.StakeKey,
                    Slot = block.Slot,
                    TxHash = stakePosition.TxHash,
                    TxIndex = stakePosition.TxIndex,
                    Amount = stakePosition.Amount,
                    StakePosition = stakePosition.StakePosition,
                    LockTime = stakePosition.LockTime,
                    Interest = stakePosition.Interest,
                    UtxoStatus = UtxoStatus.Spent
                };

                _dbContext.StakePositionByStakeKeys.Add(stakePositionByKey);
            }
        }
    }

    private async Task ProcessOutputAync(PallasDotnet.Models.Block block, TransactionBody tx)
    {
        foreach (TransactionOutput output in tx.Outputs)
        {
            string addressBech32 = output.Address.ToBech32();
            if (addressBech32.StartsWith("addr"))
            {
                Address address = new(output.Address.ToBech32());
                string pkh = Convert.ToHexString(address.GetPublicKeyHash()).ToLowerInvariant();
                if (pkh == configuration["CoinectaTimelockValidatorHash"])
                {
                    if (output.Datum is not null && output.Datum.Type == PallasDotnet.Models.DatumType.InlineDatum)
                    {
                        byte[] datum = output.Datum.Data;
                        try
                        {
                            CIP68<Timelock> timelockDatum = CborConverter.Deserialize<CIP68<Timelock>>(datum);
                            Cardano.Sync.Data.Models.TransactionOutput entityUtxo = Utils.MapTransactionOutputEntity(tx.Id.ToHex(), block.Slot, output);
                            if (entityUtxo.Amount.MultiAsset.TryGetValue(configuration["CoinectaStakeKeyPolicyId"]!, out Dictionary<string, ulong>? stakeKeyBundle))
                            {
                                string? assetName = stakeKeyBundle.Keys.FirstOrDefault(key => key.StartsWith("000643b0"));
                                if (assetName is not null)
                                {

                                    StakeRequestByAddress? stakeRequest = null;

                                    while (stakeRequest is null)
                                    {
                                        foreach (TransactionInput input in tx.Inputs)
                                        {
                                            stakeRequest = await _dbContext.StakeRequestByAddresses.FirstOrDefaultAsync(s => s.TxHash == input.Id.ToHex() && s.TxIndex == input.Index);
                                            if (stakeRequest is not null)
                                            {
                                                break;
                                            }
                                            await Task.Delay(100);
                                        }
                                    }

                                    StakePositionByStakeKey stakePositionByKey = new()
                                    {
                                        StakeKey = configuration["CoinectaStakeKeyPolicyId"]! + assetName.Replace("000643b0", string.Empty),
                                        Slot = block.Slot,
                                        TxHash = tx.Id.ToHex(),
                                        TxIndex = output.Index,
                                        Amount = entityUtxo.Amount,
                                        StakePosition = timelockDatum,
                                        LockTime = timelockDatum.Extra.Lockuntil,
                                        Interest = stakeRequest.StakePoolProxy.RewardMultiplier,
                                        UtxoStatus = UtxoStatus.Unspent
                                    };

                                    _dbContext.StakePositionByStakeKeys.Add(stakePositionByKey);
                                }
                            }
                        }
                        catch
                        {
                            _logger.LogError("Error deserializing timelock datum: {datum} for {txHash}#{txIndex}",
                                Convert.ToHexString(datum).ToLowerInvariant(),
                                tx.Id.ToHex(),
                                output.Index
                            );
                        }
                    }
                }
            }
        }

    }
}