
using Coinecta.Data.Models;
using Coinecta.Data.Models.Entity;
using Microsoft.EntityFrameworkCore;

namespace Coinecta.Data.Extensions;

public static class VestingTreasuryByIdExtension
{
    public static async Task<VestingTreasuryById?> FetchOutref(this DbSet<VestingTreasuryById> self, OutputReference outRef) =>
        await self.Where(vtbi => vtbi.TxHash + vtbi.TxIndex == (outRef.TxHash + outRef.Index).ToLower()).FirstOrDefaultAsync();

    public static async Task<VestingTreasuryById?> FetchId(this DbSet<VestingTreasuryById> self, string id) =>
        await self.Where(vtbi => vtbi.Id == id).FirstOrDefaultAsync();
}