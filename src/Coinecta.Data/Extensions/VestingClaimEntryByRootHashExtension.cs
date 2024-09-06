
using Coinecta.Data.Models;
using Coinecta.Data.Models.Entity;
using Microsoft.EntityFrameworkCore;

namespace Coinecta.Data.Extensions;

public static class VestingClaimEntryByRootHashExtension
{
    public static async Task<VestingClaimEntryByRootHash?> FetchIdAsync(this DbSet<VestingClaimEntryByRootHash> self, string id) =>
        await self.Where(vtbrh => vtbrh.Id == id).FirstOrDefaultAsync();

    public static async Task<bool> IsRootHashExistsAsync(this DbSet<VestingClaimEntryByRootHash> self, string rootHash) =>
        await self.AnyAsync(vtbrh => vtbrh.RootHash == rootHash.ToLower());
}