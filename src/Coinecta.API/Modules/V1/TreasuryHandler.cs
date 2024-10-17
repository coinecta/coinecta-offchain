using System.Linq.Expressions;
using System.Text.Json;
using Cardano.Sync.Extensions;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Utilities;
using Chrysalis.Cardano.Models.Sundae;
using Chrysalis.Cbor;
using Coinecta.Data.Extensions;
using Coinecta.Data.Extensions.Chrysalis;
using Coinecta.Data.Models;
using Coinecta.Data.Models.Api.Request;
using Coinecta.Data.Models.Api.Response;
using Coinecta.Data.Models.Entity;
using Coinecta.Data.Services;
using Coinecta.Data.Utils;
using Microsoft.EntityFrameworkCore;
using CAddress = CardanoSharp.Wallet.Models.Addresses.Address;
using ClaimEntry = Chrysalis.Cardano.Models.Coinecta.Vesting.ClaimEntry;
using CoinectaClaimEntry = Coinecta.Data.Models.ClaimEntry;
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

    public async Task<List<ClaimEntryResponse>> FetchClaimEntriesByAddressesAsync(List<string> addresses)
    { 
        await using CoinectaDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        List<string> activeRootHashes = await dbContext.VestingTreasuryById
            .AsNoTracking()
            .GroupBy(vtid => vtid.RootHash)
            .Select(group => group.Key)
            .ToListAsync();

        List<string> unlistedRootHashes = await dbContext.VestingTreasuryById
            .AsNoTracking()
            .Where(vtid => !dbContext.VestingClaimEntryByRootHash.Any(vtrh => vtrh.RootHash == vtid.RootHash))
            .Select(vtid => vtid.RootHash)
            .GroupBy(rootHash => rootHash)
            .Select(group => group.Key)
            .ToListAsync();

        string mpfBucket = configuration["MpfBucket"]!;

        if (unlistedRootHashes.Count > 0)
        {
            IEnumerable<Task> mpfUploads = unlistedRootHashes.Select(async rootHash => {
                string? mpfRawData = await s3Service.DownloadJsonAsync(mpfBucket, rootHash);
                
                if (mpfRawData != null)
                {
                    CreateTreasuryTrieRequest treasuryTrieRequest = JsonSerializer.Deserialize<CreateTreasuryTrieRequest>(mpfRawData!) 
                        ?? throw new Exception("Invalid MPF data");

                    string result = await ExecuteCreateTrieAsync(treasuryTrieRequest);
                }
            });

            await Task.WhenAll(mpfUploads);
        }

        List<string> pkHashes = addresses.Select(address => new CAddress(address).GetPublicKeyHash())
            .Select(cAddress => Convert.ToHexString(cAddress))
            .ToList();

        List<string> stakeHashes = addresses.Select(address => new CAddress(address).GetStakeKeyHash())
            .Select(cAddress => Convert.ToHexString(cAddress))
            .ToList();

        Expression<Func<VestingClaimEntryByRootHash, bool>> claimantPkhPredicate = PredicateBuilder.False<VestingClaimEntryByRootHash>();

        pkHashes.ForEach(pkHash =>
            claimantPkhPredicate = claimantPkhPredicate.Or(vtrh => vtrh.ClaimantPkh == pkHash.ToLower())
        );

        stakeHashes.ForEach(pkHash =>
            claimantPkhPredicate = claimantPkhPredicate.Or(vtrh => vtrh.ClaimantPkh == pkHash.ToLower())
        );

        List<ClaimEntryResponse> claimEntries = await dbContext.VestingClaimEntryByRootHash
            .AsNoTracking()
            .Where(claimantPkhPredicate)
            .Join(
                dbContext.VestingTreasuryById.AsNoTracking(),
                vtrh => vtrh.RootHash,
                vtid => vtid.RootHash,
                (vtrh, vtid) => new ClaimEntryResponse
                (
                    vtid.Id,
                    vtrh.RootHash,
                    vtrh.ClaimantPkh,
                    vtrh.ClaimEntry != null ? vtrh.ClaimEntry.VestingValue.ToDictionary() : new(),
                    vtrh.ClaimEntry != null ? vtrh.ClaimEntry.DirectValue.ToDictionary() : new()
                )
            )
            .ToListAsync();

        claimEntries = claimEntries
            .Where(ce => 
                (ce.VestingValue != null && ce.VestingValue.Values.Any(v => v.Values.Any(amount => amount > 0))) || 
                (ce.DirectValue != null && ce.DirectValue.Values.Any(d => d.Values.Any(amount => amount > 0)))
            )
            .ToList();

        return claimEntries;
    }

    public async Task<TreasuryTrieData?> ConvertCsvToTreasuryTrieDataAsync(HttpRequest request)
    {   
        if (!request.HasFormContentType) return null;

        IFormCollection form = await request.ReadFormAsync();
        IFormFile? file = form.Files.FirstOrDefault();
        
        if (file == null || file.Length == 0) return null;

        StreamReader reader = new StreamReader(file.OpenReadStream());
        string csvData = await reader.ReadToEndAsync();

        string[] lines = csvData.Split(['\n', '\r'])[1..];

        if (lines.Length == 0) return null;

        Dictionary<string, CoinectaClaimEntry> claimEntries = [];

        foreach (string line in lines)
        {
            string[] fields = line.Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length == 0) continue;

            string stakeAddress = fields.First();

            if (!ulong.TryParse(fields[1..].First(), out ulong lovelace)) continue;

            Dictionary<string, Dictionary<string, ulong>> multiAsset = [];

            string policyId = "";
            string assetName = "";
            bool isInAmountField = false;
            
            foreach (string field in fields[2..])
            {
                if (!isInAmountField)
                {
                    policyId = field[..56];
                    assetName = field[56..];

                    isInAmountField = true;
                    continue;
                }

                if (!double.TryParse(field, out double amount)) continue;

                ulong actualAmount = (ulong)RemoveDecimalPoint(amount);

                multiAsset.Add(policyId, new()
                {
                    {assetName, actualAmount}
                });

                isInAmountField = false;
            }

            Value directValue = new()
            {
                Coin = lovelace,
                MultiAsset = multiAsset
            };

            CoinectaClaimEntry claimEntry = new
            (
                VestingValue: default,
                DirectValue: directValue,
                VestingProgramScriptHash: default,
                VestingParameters: default
            );

            claimEntries.Add(stakeAddress, claimEntry);
        }

        if (claimEntries.Count() == 0) return null;

        TreasuryTrieData treasuryTrieData = new
        (
            ClaimEntries: claimEntries,
            VestingProgramScriptHash: "00",
            VestingParameters: "00"
        );

        return treasuryTrieData;
    }

    public async Task<IResult> CreateTrieFromCsvAsync(HttpRequest request)
    {
        TreasuryTrieData? treasuryTrieData = await ConvertCsvToTreasuryTrieDataAsync(request);

        if (treasuryTrieData is null)
        {
            return Results.BadRequest("Failed to convert csv to treasury trie data");
        }

        CreateTreasuryTrieRequest treasuryTrieRequest = new(treasuryTrieData);
        IResult result = await CreateTrieAsync(treasuryTrieRequest);

        return result;
    }

    private double RemoveDecimalPoint(double value)
    {
        string valueString = value.ToString();
        
        if (!valueString.Contains('.')) return value;

        string cleanedValueString = valueString.Replace(".", "");
        
        return double.Parse(cleanedValueString);
    }
}