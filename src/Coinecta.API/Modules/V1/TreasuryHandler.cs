using System.Text.Json;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Utilities;
using Chrysalis.Cardano.Models.Sundae;
using Chrysalis.Cbor;
using Coinecta.Data.Extensions;
using Coinecta.Data.Models;
using Coinecta.Data.Models.Api.Request;
using Coinecta.Data.Models.Api.Response;
using Coinecta.Data.Models.Entity;
using Coinecta.Data.Services;
using Coinecta.Data.Utils;
using Microsoft.EntityFrameworkCore;
using CAddress = CardanoSharp.Wallet.Models.Addresses.Address;
using ClaimEntry = Chrysalis.Cardano.Models.Coinecta.Vesting.ClaimEntry;
using Signature = Chrysalis.Cardano.Models.Sundae.Signature;
namespace Coinecta.API.Modules.V1;

public class TreasuryHandler(
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    MpfService mpfService,
    S3Service s3Service
)
{
    private NetworkType Network => NetworkUtils.GetNetworkType(configuration);
    public async Task<IResult> CreateTrieAsync(CreateTreasuryTrieRequest request)
    {
        string rootHash = await ExecuteCreateTrieAsync(request);
        return Results.Ok(rootHash);
    }

    public async Task<IResult> PrepareClaimDataAsync(string rootHash, string ownerAddress)
    {
        CAddress ownerAddr = new(ownerAddress);

        // If claim entry exists, get latest mpf data, proof and updated roothash
        string mpfBucket = configuration["MpfBucket"]!;
        string? mpfRawData = await s3Service.DownloadJsonAsync(mpfBucket, rootHash);
        CreateTreasuryTrieRequest treasuryTrieRequest = JsonSerializer.Deserialize<CreateTreasuryTrieRequest>(mpfRawData!) ?? throw new Exception("Invalid MPF data");

        // Fetch the proof and the original mpf data
        MultisigScript ownerSignature = new Signature(new(ownerAddr.GetPublicKeyHash()));
        Dictionary<string, string> mpfData = treasuryTrieRequest.Data.ToMpfRequest().Data;
        byte[] claimRawKey = CborSerializer.Serialize(ownerSignature);
        string claimKey = Convert.ToHexString(claimRawKey).ToLowerInvariant();
        string proofRaw = await mpfService.GetProofAsync(new(mpfData, claimKey));

        // Create updated mpf trie
        string originalRawClaimEntry = mpfData[claimKey];
        ClaimEntry claimEntry = CborSerializer.Deserialize<ClaimEntry>(Convert.FromHexString(originalRawClaimEntry))!;
        ClaimEntry updatedClaimEntry = claimEntry with
        {
            DirectValue = new([]),
            VestingValue = new([])
        };
        string updatedRawClaimEntry = Convert.ToHexString(CborSerializer.Serialize(updatedClaimEntry));
        mpfData[claimKey] = updatedRawClaimEntry;

        treasuryTrieRequest.Data.ClaimEntries[ownerAddr.ToString()] = treasuryTrieRequest.Data.ClaimEntries[ownerAddr.ToString()] with
        {
            DirectValue = new(),
            VestingValue = new()
        };

        CreateTreasuryTrieRequest updatedreasuryTrieRequest = treasuryTrieRequest with
        {
            Data = treasuryTrieRequest.Data
        };

        string updatedRootHash = await ExecuteCreateTrieAsync(updatedreasuryTrieRequest);

        return Results.Ok(new ClaimDataResponse(updatedRootHash, proofRaw, originalRawClaimEntry));
    }

    public async Task<IResult> FetchLatestTreasuryRootHashByIdAsync(string id)
    {
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

        // Fetch confirmed treasury state
        VestingTreasuryById? vestingTreasuryById = await dbContext.VestingTreasuryById.FetchIdAsync(id);

        if (vestingTreasuryById is null) return Results.NotFound();

        // Fetch pending treasury state if any
        ulong currentSlot = (ulong)SlotUtility.GetSlotFromUTCTime(SlotUtility.GetSlotNetworkConfig(Network), DateTime.UtcNow);
        VestingTreasuryById latestVestingTreasuryById = 
            await dbContext.VestingTreasurySubmittedTxs
                .FetchConfirmedOrPendingVestingTreasuryAsync(vestingTreasuryById, currentSlot, 3);

        return Results.Ok(latestVestingTreasuryById.RootHash);
    }

    public async Task<string> ExecuteCreateTrieAsync(CreateTreasuryTrieRequest request)
    {
        // Get the trie root hash using MPF api
        CreateMpfRequest mpfRequest = request.Data.ToMpfRequest();
        string rootHash = await mpfService.CreateAsync(mpfRequest);

        // Upload trie to S3
        await UploadClaimEntriesToS3Async(rootHash, request);

        // Save the claim entries to the database
        // @TODO: only save when tx successfully submitted
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();
        if (!await dbContext.VestingClaimEntryByRootHash.IsRootHashExistsAsync(rootHash))
        {
            List<KeyValuePair<string, string>> claimEntries = [.. mpfRequest.Data];
            await SaveClaimEntriesToDbAsync(claimEntries, rootHash);
        }

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