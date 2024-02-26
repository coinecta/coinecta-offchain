using Microsoft.EntityFrameworkCore;
using PallasDotnet.Models;
using Coinecta.Data;
using Address = CardanoSharp.Wallet.Models.Addresses.Address;
using CardanoSharp.Wallet.Extensions.Models;
using CborSerialization;
using Coinecta.Data.Models.Datums;
using Coinecta.Data.Models.Reducers;

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
        var rollbackSlot = response.Block.Slot;
        var schema = configuration.GetConnectionString("CoinectaContextSchema");
        await _dbContext.Database.ExecuteSqlRawAsync($"DELETE FROM \"{schema}\".\"StakePositionByStakeKeys\" WHERE \"Slot\" > {rollbackSlot}");
        _dbContext.Dispose();
    }

    public async Task RollForwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();
        await ProcessInputAync(response);
        await ProcessOutputAync(response);
        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }

    private async Task ProcessInputAync(NextResponse response)
    {
        // Collect input id and index as tuples
        var inputTuples = response.Block.TransactionBodies
            .SelectMany(txBody => txBody.Inputs.Select(input => (Id: input.Id.ToHex(), input.Index)))
            .ToList();

        // Define the base query
        var query = _dbContext.StakePositionByStakeKeys.AsQueryable();

        // Build the query dynamically with OR conditions
        IQueryable<StakePositionByStakeKey>? combinedQuery = null;
        foreach (var tuple in inputTuples)
        {
            var currentQuery = query.Where(s => s.TxHash == tuple.Id && s.TxIndex == tuple.Index);
            combinedQuery = combinedQuery == null ? currentQuery : combinedQuery.Union(currentQuery);
        }

        // Execute the query if there are conditions
        if (combinedQuery is not null)
        {
            var stakePositionsByStakeKey = await combinedQuery.ToListAsync();
            if (stakePositionsByStakeKey.Count != 0)
            {
                _dbContext.StakePositionByStakeKeys.RemoveRange(stakePositionsByStakeKey);
            }
        }
    }

    private async Task ProcessOutputAync(NextResponse response)
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
                    if (pkh == configuration["CoinectaTimelockValidatorHash"])
                    {
                        if (output.Datum is not null && output.Datum.Type == DatumType.InlineDatum)
                        {
                            var datum = output.Datum.Data;
                            try
                            {
                                var timelockDatum = CborConverter.Deserialize<CIP68<Timelock>>(datum);
                                var entityUtxo = Utils.MapTransactionOutputEntity(txBody.Id.ToHex(), response.Block.Slot, output);
                                if (entityUtxo.Amount.MultiAsset.TryGetValue(configuration["CoinectaStakeKeyPolicyId"]!, out Dictionary<string, ulong>? stakeKeyBundle))
                                {
                                    var assetName = stakeKeyBundle.Keys.FirstOrDefault(key => key.StartsWith("000643b0"));
                                    if (assetName is not null)
                                    {
                                        
                                        StakeRequestByAddress? stakeRequest = null;

                                        while(stakeRequest is null)
                                        {
                                            foreach(var input in txBody.Inputs)
                                            {
                                                stakeRequest = await _dbContext.StakeRequestByAddresses.FirstOrDefaultAsync(s => s.TxHash == input.Id.ToHex() && s.TxIndex == input.Index);
                                                if (stakeRequest is not null)
                                                {
                                                    break;
                                                }
                                                await Task.Delay(100);
                                            }
                                        }

                                        var stakePositionByKey = new StakePositionByStakeKey
                                        {
                                            StakeKey = configuration["CoinectaStakeKeyPolicyId"]! + assetName.Replace("000643b0", string.Empty),
                                            Slot = response.Block.Slot,
                                            TxHash = txBody.Id.ToHex(),
                                            TxIndex = output.Index,
                                            Amount = entityUtxo.Amount,
                                            StakePosition = timelockDatum,
                                            LockTime = timelockDatum.Extra.Lockuntil,
                                            Interest = stakeRequest.StakePoolProxy.RewardMultiplier
                                        };

                                        _dbContext.StakePositionByStakeKeys.Add(stakePositionByKey);
                                    }
                                }
                            }
                            catch
                            {
                                _logger.LogError("Error deserializing timelock datum: {datum} for {txHash}#{txIndex}",
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
    }
}