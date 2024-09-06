using System.Text.Json;
using Chrysalis.Cardano.Models.Coinecta.Vesting;
using Chrysalis.Cardano.Models.Sundae;
using Chrysalis.Cbor;
using Coinecta.Data.Extensions;
using Coinecta.Data.Models;
using Coinecta.Data.Models.Api.Request;
using Coinecta.Data.Models.Entity;
using Coinecta.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace Coinecta.API.Modules.V1;

public class TreasuryHandler(
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    MpfService mpfService,
    S3Service s3Service
)
{
    public async Task<string> CreateTrieAsync(CreateTreasuryTrieRequest request)
    {
        // Get the trie root hash using MPF api
        CreateMpfRequest mpfRequest = request.Data.ToMpfRequest();
        string rootHash = await mpfService.CreateAsync(mpfRequest);

        // Upload trie to S3
        await UploadClaimEntriesToS3Async(rootHash, request);

        // Save the claim entries to the database
        // @TODO: only save when tx successfully submitted
        List<KeyValuePair<string, string>> claimEntries = [.. mpfRequest.Data];
        await SaveClaimEntriesToDbAsync(claimEntries, rootHash);

        return rootHash;
    }

    private async Task UploadClaimEntriesToS3Async(string rootHash, CreateTreasuryTrieRequest request)
    {
        string mpfBucket = configuration["MpfBucket"] ?? "Mpf bucket not configured";
        await s3Service.UploadJsonAsync(mpfBucket, rootHash, JsonSerializer.Serialize(request));
    }

    private async Task SaveClaimEntriesToDbAsync(List<KeyValuePair<string, string>> claimEntries, string rootHash)
    {
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

        claimEntries.ForEach(entry =>
        {
            MultisigScript ownerSignature = CborSerializer.Deserialize<MultisigScript>(Convert.FromHexString(entry.Key))!;
            string ownerPkh = ownerSignature switch
            {
                Signature sig => Convert.ToHexString(sig.KeyHash.Value).ToLowerInvariant(),
                _ => throw new Exception("Only signature is currently supported")
            };

            byte[] claimEntryRaw = Convert.FromHexString(entry.Value);

            // Create a new database claim entry
            VestingClaimEntryByRootHash dbEntry = new()
            {
                Id = rootHash + ownerPkh,
                RootHash = rootHash,
                ClaimantPkh = ownerPkh,
                ClaimEntryRaw = claimEntryRaw
            };

            dbContext.Add(dbEntry);
        });

        await dbContext.SaveChangesAsync();
        await dbContext.DisposeAsync();
    }
}