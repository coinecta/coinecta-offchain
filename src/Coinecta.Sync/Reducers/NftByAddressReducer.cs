using Cardano.Sync.Reducers;
using Coinecta.Data;
using Coinecta.Data.Models.Enums;
using Coinecta.Data.Models.Reducers;
using Microsoft.EntityFrameworkCore;
using PallasDotnet.Models;

namespace Coinecta.Sync.Reducers;

public class NftByAddressReducer(
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    ILogger<NftByAddressReducer> logger
) : IReducer
{
    private readonly string _stakeKeyPolicyId = configuration["CoinectaStakeKeyPolicyId"]!;
    private readonly string _stakeKeyPrefix = configuration["StakeKeyPrefix"]!;
    private CoinectaDbContext _dbContext = default!;
    private readonly ILogger<NftByAddressReducer> _logger = logger;

    public async Task RollBackwardAsync(NextResponse response)
    {
        using CoinectaDbContext _dbContext = dbContextFactory.CreateDbContext();

        // Remove all entries with slot greater than the rollback slot
        ulong rollbackSlot = response.Block.Slot;
        IQueryable<NftByAddress> rollbackEntries = _dbContext.NftsByAddress.AsNoTracking().Where(lba => lba.Slot > rollbackSlot);
        _dbContext.NftsByAddress.RemoveRange(rollbackEntries);

        // Save changes
        await _dbContext.SaveChangesAsync();
        await _dbContext.DisposeAsync();
    }

    public async Task RollForwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();
        IEnumerable<TransactionBody> transactions = response.Block.TransactionBodies;

        foreach (var tx in transactions)
        {
            await ProcessInputAync(response.Block, tx);
            await ProcessOutputAync(response.Block, tx);
        }

        await _dbContext.SaveChangesAsync();
        await _dbContext.DisposeAsync();
    }

    private async Task ProcessInputAync(Block block, TransactionBody tx)
    {
        foreach (TransactionInput input in tx.Inputs)
        {
            // First check in-memory data
            List<NftByAddress> nftByAddresses = _dbContext.NftsByAddress.Local
                .Where(s => s.TxHash == input.Id.ToHex())
                .Where(s => s.OutputIndex == input.Index)
                .ToList();

            // Then check the database
            nftByAddresses = nftByAddresses.Count > 0 ? nftByAddresses : await _dbContext.NftsByAddress
                .Where(s => s.TxHash == input.Id.ToHex())
                .Where(s => s.OutputIndex == input.Index)
                .ToListAsync();

            nftByAddresses.ForEach(nftByAddress =>
            {
                NftByAddress spentNftByAddress = new()
                {
                    Address = nftByAddress.Address,
                    TxHash = nftByAddress.TxHash,
                    OutputIndex = nftByAddress.OutputIndex,
                    Slot = block.Slot,
                    PolicyId = nftByAddress.PolicyId,
                    AssetName = nftByAddress.AssetName,
                    UtxoStatus = UtxoStatus.Spent
                };

                _dbContext.NftsByAddress.Add(spentNftByAddress);
            });
        }
    }

    private Task ProcessOutputAync(Block block, TransactionBody tx)
    {
        tx.Outputs.ToList().ForEach(output =>
        {
            string addressBech32 = output.Address.ToBech32();
            if (addressBech32.StartsWith("addr"))
            {
                var assets = output.Amount.MultiAsset
                    .ToDictionary(k => k.Key.ToHex(), v => v.Value.ToDictionary(
                        k => k.Key.ToHex(),
                        v => v.Value
                    ))
                    .SelectMany(outer => outer.Value, (outer, inner) => (outer.Key, inner.Key, inner.Value))
                    .ToList();

                foreach (var (policyId, assetName, amount) in assets)
                {
                    if (policyId == _stakeKeyPolicyId && assetName.StartsWith(_stakeKeyPrefix))
                    {
                        var nftByAddress = new NftByAddress
                        {
                            Address = addressBech32,
                            TxHash = tx.Id.ToHex(),
                            OutputIndex = output.Index,
                            Slot = block.Slot,
                            PolicyId = policyId,
                            AssetName = assetName,
                            UtxoStatus = UtxoStatus.Unspent
                        };

                        _dbContext.NftsByAddress.Add(nftByAddress);
                    }
                }
            }
        });

        return Task.CompletedTask;
    }
}