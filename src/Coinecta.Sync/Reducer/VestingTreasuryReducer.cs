using Cardano.Sync.Reducers;
using Coinecta.Data.Models;
using Microsoft.EntityFrameworkCore;
using PallasDotnet.Models;
using PallasAddress = PallasDotnet.Models.Address;
using CardanoSharpAddress = CardanoSharp.Wallet.Models.Addresses.Address;
using CardanoSharp.Wallet.Extensions.Models;
using Coinecta.Data.Models.Entity;
using Chrysalis.Cbor;
using Coinecta.Data.Extensions;
using Coinecta.Data.Models.Enums;
using TransactionBody = PallasDotnet.Models.TransactionBody;
using Chrysalis.Cardano.Models.Coinecta.Vesting;
using Chrysalis.Cardano.Models.Sundae;
using Cardano.Sync.Extensions;
using System.Linq.Expressions;

namespace Coinecta.Sync.Reducer;

public class VestingTreasuryReducer(
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    ILogger<VestingTreasuryReducer> logger
) : IReducer
{
    private readonly string _treasuryValidatorScriptHash = configuration["TreasuryValidatorScriptHash"]!;
    private readonly string _treasuryIdMintingPolicy = configuration["TreasuryIdMintingPolicy"]!;

    public async Task RollBackwardAsync(NextResponse response)
    {
        using CoinectaDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        // Get all affected vesting treasury by ID entries before deleting both unspent and historical data
        IQueryable<VestingTreasuryById> rollbackVestingTreasuryByIdEntries = dbContext.VestingTreasuryById
            .Where(vtbi => vtbi.Slot > response.Block.Slot);

        // Delete all affected vesting treasury by slot entries
        IQueryable<VestingTreasuryBySlot> rollbackVestingTreasuryBySlotEntries = dbContext.VestingTreasuryBySlot
            .Where(vtbs => vtbs.Slot > response.Block.Slot);

        // Remove affected entries
        dbContext.RemoveRange(rollbackVestingTreasuryByIdEntries);
        dbContext.RemoveRange(rollbackVestingTreasuryBySlotEntries);

        // Find the last state of the affected vesting treasury by ID entries
        IQueryable<string> affectedIds = rollbackVestingTreasuryByIdEntries.Select(rvtbi => rvtbi.Id);

        // Get the previous state of the affected entries
        List<VestingTreasuryBySlot> prevStateTreasuryBySlotEntries = await dbContext.VestingTreasuryBySlot
            .AsNoTracking()
            .Where(vtbs => vtbs.Slot <= response.Block.Slot)
            .Where(vtbs => affectedIds.Contains(vtbs.Id))
            .GroupBy(vtbs => new { vtbs.Id, vtbs.TxHash, vtbs.TxIndex })
            .Where(g => g.Count() < 2)
            .Select(g => g.First())
            .ToListAsync();

        // Revert to the previous state
        IEnumerable<VestingTreasuryById> vestingTreasuryByIdEntries = prevStateTreasuryBySlotEntries.Select(vtbs => new VestingTreasuryById()
        {
            Id = vtbs.Id,
            Slot = vtbs.Slot,
            TxHash = vtbs.TxHash,
            TxIndex = vtbs.TxIndex,
            UtxoRaw = vtbs.UtxoRaw,
            OwnerPkh = vtbs.OwnerPkh
        });

        dbContext.AddRange(vestingTreasuryByIdEntries);

        // Save changes and dispose
        await dbContext.SaveChangesAsync();
        await dbContext.DisposeAsync();
    }

    public async Task RollForwardAsync(NextResponse response)
    {
        using CoinectaDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        // Process Outputs
        foreach (TransactionBody tx in response.Block.TransactionBodies)
            await ProcessOutputs(tx, response.Block, dbContext);

        // Get all input outrefs
        List<((string TxHash, ulong TxIndex) Id, Redeemer? Redeemer)> inputsTuple = response.Block.TransactionBodies
            .SelectMany(tx => tx.Inputs, (tx, input) => ((input.Id.ToHex(), input.Index), tx.Redeemers?.ToList().Find(r => r.Index == input.Index)))
            .ToList();

        IEnumerable<string> inputOutRefs = inputsTuple.Select(inputTuple => inputTuple.Id.TxHash + inputTuple.Id.TxIndex);

        Expression<Func<VestingTreasuryBySlot, bool>> predicate = PredicateBuilder.False<VestingTreasuryBySlot>();

        inputsTuple.ForEach(inputTuple =>
            predicate = predicate.Or(vtbs => vtbs.TxHash == inputTuple.Id.TxHash && vtbs.TxIndex == inputTuple.Id.TxIndex)
        );

        // Fetch all the corresponding entry from the database, if there's any
        // Note: an output can be spent within the same block, so we also need to check
        // all outputs processed in this block
        List<VestingTreasuryBySlot> vestingTreasuryBySlotEntries = await dbContext.VestingTreasuryBySlot
            .AsNoTracking()
            .Where(predicate)
            .ToListAsync();

        List<VestingTreasuryBySlot> vestingTreasuryBySlotLocalEntries = dbContext.VestingTreasuryBySlot.Local
            .Where(vtbs => inputOutRefs.Contains(vtbs.TxHash + vtbs.TxIndex))
            .ToList();

        // Merge
        vestingTreasuryBySlotEntries.AddRange(vestingTreasuryBySlotLocalEntries);

        IEnumerable<(VestingTreasuryBySlot vtbs, Redeemer? Redeemer)> vestingTreasuryBySlotRedeemerTuple = vestingTreasuryBySlotEntries
            .Select(vtbs => (vtbs, inputsTuple
                .Where(it => it.Id.TxHash == vtbs.TxHash && it.Id.TxIndex == vtbs.TxIndex)
                .FirstOrDefault().Redeemer)
            );

        await ProcessInputs(vestingTreasuryBySlotRedeemerTuple, response.Block, dbContext);

        // Before saving to database, we need to update VestingTreasuryById to reflect
        // the updated state. A vesting treasury entry is unspent if there is an output outref with
        // no corresponding input outref

        // First we find all spent vesting treasury entries so we can remove them from the database
        List<string> spentVestingTreasuryOutrefs = dbContext.VestingTreasuryBySlot.Local
            .Where(vtbs => vtbs.UtxoStatus == UtxoStatus.Spent)
            .Select(vtbs => vtbs.TxHash + vtbs.TxIndex)
            .ToList();

        IQueryable<VestingTreasuryById> spentVestingTreasuryByIdEntries = dbContext.VestingTreasuryById
            .Where(vtbi => spentVestingTreasuryOutrefs.Contains(vtbi.TxHash + vtbi.TxIndex));

        // Delete the spent entries
        dbContext.VestingTreasuryById.RemoveRange(spentVestingTreasuryByIdEntries);

        // Then we add the updated unspent vesting treasury by id entries
        // We need to get all outrefs with no corresponding spent entry
        List<VestingTreasuryBySlot> unspentVestingTreasuryBySlotEntries = dbContext.VestingTreasuryBySlot.Local
            .GroupBy(vtbs => new { vtbs.TxHash, vtbs.TxIndex })
            .Where(g => g.Count() < 2)
            .Select(g => g.First())
            .Where(g => g.UtxoStatus != UtxoStatus.Spent)
            .ToList();

        IEnumerable<VestingTreasuryById> vestingTreasuryByIdEntries = unspentVestingTreasuryBySlotEntries.Select(vtbs => new VestingTreasuryById()
        {
            Id = vtbs.Id,
            Slot = vtbs.Slot,
            TxHash = vtbs.TxHash,
            TxIndex = vtbs.TxIndex,
            UtxoRaw = vtbs.UtxoRaw,
            OwnerPkh = vtbs.OwnerPkh
        });

        // Insert into database
        dbContext.VestingTreasuryById.AddRange(vestingTreasuryByIdEntries);

        // Save into the database
        await dbContext.SaveChangesAsync();
        await dbContext.DisposeAsync();
    }

    private Task ProcessInputs(IEnumerable<(VestingTreasuryBySlot Vtbs, Redeemer? Redeemer)> vestingTreasuryBySlotsTuple, Block block, CoinectaDbContext dbContext)
    {
        vestingTreasuryBySlotsTuple.ToList().ForEach(vtbsTuple =>
        {

            // Check if there's a redeemer
            byte[]? redeemer = vtbsTuple.Redeemer?.Data;
            TreasuryActionType actionType = TreasuryActionType.Create;

            if (redeemer is not null)
            {
                try
                {
                    TreasuryRedeemer? treasuryRedeemer = CborSerializer.Deserialize<TreasuryRedeemer>(redeemer);

                    if (treasuryRedeemer is not null)
                    {
                        actionType = treasuryRedeemer switch
                        {
                            TreasuryWithdrawRedeemer => TreasuryActionType.Withdraw,
                            TreasuryClaimRedeemer => TreasuryActionType.Claim,
                            _ => TreasuryActionType.Create
                        };
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                }
            }

            VestingTreasuryBySlot updatedEntry = vtbsTuple.Vtbs with
            {
                Slot = (uint)block.Slot,
                BlockHash = block.Hash.ToHex(),
                UtxoStatus = UtxoStatus.Spent,
                Type = actionType
            };

            dbContext.Add(updatedEntry);
        });

        return Task.CompletedTask;
    }

    private Task ProcessOutputs(TransactionBody tx, Block block, CoinectaDbContext dbContext)
    {
        tx.Outputs.ToList().ForEach(output =>
         {
             PallasAddress _address = output.Address;
             string _addressBech32 = _address.ToBech32();

             if (_addressBech32.StartsWith("addr_test1") || _addressBech32.StartsWith("addr1"))
             {
                 // If it is not a treasury validator output, then ignore
                 CardanoSharpAddress address = new(_address.ToBech32());
                 string pkh = Convert.ToHexString(address.GetPublicKeyHash());
                 if (!pkh.Equals(_treasuryValidatorScriptHash, StringComparison.InvariantCultureIgnoreCase)) return;

                 // Check if there is an identifier asset in the output, otherwise ignore
                 IEnumerable<(string policyId, string assetName, ulong quantity)> assets = output.Amount.MultiAsset.ToStringKeys().Flatten();
                 if (!assets.Select(a => a.policyId).ToList().Contains(_treasuryIdMintingPolicy)) return;

                 // Check if there is a datum, otherwise ignore
                 if (output.Datum is null || output.Datum.Data is null) return;
                 try
                 {
                     // Check if datum is correct shape
                     Treasury? datum = CborSerializer.Deserialize<Treasury>(output.Datum.Data);
                     if (datum is null) return;

                     string treasuryOwnerPkh = datum.Owner switch
                     {
                         Signature sig => Convert.ToHexString(sig.KeyHash.Value).ToLowerInvariant(),
                         _ => throw new Exception("Only signature is currently supported")
                     };

                     // Check if there's a redeemer
                     byte[]? redeemer = tx.Redeemers?.FirstOrDefault()?.Data;
                     TreasuryActionType actionType = TreasuryActionType.Create;

                     if (redeemer is not null)
                     {
                         try
                         {
                             TreasuryRedeemer? treasuryRedeemer = CborSerializer.Deserialize<TreasuryRedeemer>(redeemer);

                             if (treasuryRedeemer is not null)
                             {
                                 actionType = treasuryRedeemer switch
                                 {
                                     TreasuryWithdrawRedeemer => TreasuryActionType.Withdraw,
                                     TreasuryClaimRedeemer => TreasuryActionType.Claim,
                                     _ => TreasuryActionType.Create
                                 };
                             }
                         }
                         catch (Exception ex)
                         {
                             logger.LogError(ex.Message);
                         }
                     }

                     (string policyId, string assetName, ulong quantity) = assets.Where(a => a.policyId == _treasuryIdMintingPolicy).First();
                     string id = policyId + assetName;

                     // If all checks pass, then we create a database entry
                     VestingTreasuryBySlot entry = new()
                     {
                         Slot = (uint)block.Slot,
                         Id = id,
                         BlockHash = block.Hash.ToHex(),
                         TxHash = tx.Id.ToHex(),
                         TxIndex = (uint)output.Index,
                         UtxoRaw = output.Raw,
                         OwnerPkh = treasuryOwnerPkh,
                         UtxoStatus = UtxoStatus.Unspent,
                         Type = actionType
                     };

                     dbContext.Add(entry);
                 }
                 catch
                 {
                     logger.LogError("Unable to deserialize treasury datum");
                 }
             }
         });

        return Task.CompletedTask;
    }
}