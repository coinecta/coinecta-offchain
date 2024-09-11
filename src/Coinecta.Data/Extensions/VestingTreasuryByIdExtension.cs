
using Chrysalis.Cardano.Models.Coinecta.Vesting;
using Coinecta.Data.Models;
using Coinecta.Data.Models.Entity;
using Microsoft.EntityFrameworkCore;

namespace Coinecta.Data.Extensions;

public static class VestingTreasuryByIdExtension
{
    public static async Task<VestingTreasuryById?> FetchOutrefAsync(this DbSet<VestingTreasuryById> self, OutputReference outRef) =>
        await self.Where(vtbi => vtbi.TxHash + vtbi.TxIndex == (outRef.TxHash + outRef.Index).ToLower()).FirstOrDefaultAsync();

    public static async Task<VestingTreasuryById?> FetchIdAsync(this DbSet<VestingTreasuryById> self, string id) =>
        await self.Where(vtbi => vtbi.Id == id).FirstOrDefaultAsync();
}