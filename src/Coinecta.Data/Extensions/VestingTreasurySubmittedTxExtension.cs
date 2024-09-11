using Chrysalis.Cardano.Models.Coinecta.Vesting;
using Coinecta.Data.Models.Entity;
using Microsoft.EntityFrameworkCore;

namespace Coinecta.Data.Extensions;

public static class VestingTreasurySubmittedTxExtension
{
    public static async Task<VestingTreasuryById> FetchConfirmedOrPendingVestingTreasuryAsync(
        this DbSet<VestingTreasurySubmittedTx> self,
        VestingTreasuryById confirmed,
        ulong currentSlot,
        int confirmationCount
    )
    {
        VestingTreasurySubmittedTx? pending = await self.Where(vtst => vtst.Id.ToLower() == confirmed.Id.ToLower())
            .OrderBy(vtst => vtst.Slot)
            .FirstOrDefaultAsync();

        if (pending is not null && pending.Slot > confirmed.Slot)
        {
            ulong expiration = 20 * confirmationCount;
            ulong slotElapsed = currentSlot - pending.Slot;

            // Check if latest mempool entry is already expired
            if (slotElapsed < expiration)
            {
                Treasury pendingTreasury = pending.TreasuryDatum!;
                string pendingRootHash = Convert.ToHexString(pendingTreasury.TreasuryRootHash.Value).ToLowerInvariant();

                confirmed = confirmed with
                {
                    TxHash = pending.TxHash,
                    TxIndex = pending.TxIndex,
                    UtxoRaw = pending.UtxoRaw,
                    RootHash = pendingRootHash,
                };
            }
        }

        return confirmed;
    }
}