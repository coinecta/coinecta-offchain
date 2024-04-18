using System.Text;
using Coinecta.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Coinecta.Data.Models.Reducers;
using Coinecta.Data.Models.Response;
using Coinecta.Data.Models.Api;
using Coinecta.Data.Models.Api.Request;
using Coinecta.Data.Services;
using Coinecta.Data.Utils;
using CardanoSharp.Wallet.Enums;
using Coinecta.Models.Api;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Utilities;
using Cardano.Sync.Data.Models.Datums;
using Cardano.Sync;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using PeterO.Cbor2;
using CardanoSharp.Wallet.CIPs.CIP2.Extensions;
using Coinecta.Data.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<CoinectaDbContext>(options =>
{
    options
    .UseNpgsql(
        builder.Configuration
        .GetConnectionString("CardanoContext"),
            x =>
            {
                x.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    builder.Configuration.GetConnectionString("CardanoContextSchema")
                );
            }
        );
});

builder.Services.AddScoped<TransactionBuildingService>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName!.Replace('.', '_'));
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new UlongToStringConverter());
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        builder =>
        {
            builder.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.

app.UseSwagger();
app.UseSwaggerUI();


app.UseHttpsRedirection();

app.MapGet("/stake/pool/{address}/{ownerPkh}/{policyId}/{assetName}", async (
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    string address,
    string ownerPkh,
    string policyId,
    string assetName
) =>
{
    using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();
    List<StakePoolByAddress> stakePools = await dbContext.StakePoolByAddresses.Where(s => s.Address == address).OrderByDescending(s => s.Slot).ToListAsync();

    return Results.Ok(
        stakePools
            .Where(sp => Convert.ToHexString(sp.StakePool.Owner.KeyHash).Equals(ownerPkh, StringComparison.InvariantCultureIgnoreCase))
            .Where(sp => sp.Amount.MultiAsset.ContainsKey(policyId.ToLowerInvariant()) && sp.Amount.MultiAsset[policyId].ContainsKey(assetName.ToLowerInvariant()))
            .GroupBy(u => new { u.TxHash, u.TxIndex }) // Group by both TxHash and TxIndex
            .Where(g => g.Count() < 2)
            .Select(g => g.First())
            .First()
    );
})
.WithName("GetStakePool")
.WithOpenApi();

app.MapGet("/stake/pools/{address}/{ownerPkh}", async (
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    string address,
    string ownerPkh
) =>
{
    using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();
    List<StakePoolByAddress> stakePools = await dbContext.StakePoolByAddresses.Where(s => s.Address == address).OrderByDescending(s => s.Slot).ToListAsync();

    return Results.Ok(
        stakePools
            .Where(sp => Convert.ToHexString(sp.StakePool.Owner.KeyHash).Equals(ownerPkh, StringComparison.InvariantCultureIgnoreCase))
            .GroupBy(u => new { u.TxHash, u.TxIndex }) // Group by both TxHash and TxIndex
            .Where(g => g.Count() < 2)
            .Select(g => g.First())
            .ToList()
    );
})
.WithName("GetStakePools")
.WithOpenApi();

app.MapPost("/stake/summary", async (IDbContextFactory<CoinectaDbContext> dbContextFactory, IConfiguration configuration, [FromBody] List<string> stakeKeys) =>
{
    if (stakeKeys.Count == 0)
    {
        return Results.BadRequest("No stake keys provided");
    }

    using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

    // Current Timestamp
    DateTimeOffset dto = new(DateTime.UtcNow);
    ulong currentTimestamp = (ulong)dto.ToUnixTimeMilliseconds();

    // Get Stake Positions
    List<StakePositionByStakeKey> stakePositions = await dbContext.StakePositionByStakeKeys.Where(s => stakeKeys.Contains(s.StakeKey)).ToListAsync();

    // Filter Stake Positions
    IEnumerable<StakePositionByStakeKey> lockedPositions = stakePositions.Where(sp => sp.LockTime > currentTimestamp);
    IEnumerable<StakePositionByStakeKey> unclaimedPositions = stakePositions.Where(sp => sp.LockTime <= currentTimestamp);

    // Transaform Stake Positions
    StakeSummaryResponse result = new();

    stakePositions.ForEach(sp =>
    {
        // Remove NFT
        sp.Amount.MultiAsset.Remove(configuration["CoinectaStakeMintingPolicyId"]!);
        bool isLocked = sp.LockTime > currentTimestamp;
        string? policyId = sp.Amount.MultiAsset.Keys.FirstOrDefault();
        Dictionary<string, ulong> asset = sp.Amount.MultiAsset[policyId!];
        string assetName = asset.Keys.FirstOrDefault()!;
        string subject = policyId + assetName;
        ulong total = asset.Values.FirstOrDefault();

        if (result.PoolStats.TryGetValue(subject, out StakeStats? value))
        {
            value.TotalStaked += total;
            value.TotalPortfolio += total;
            value.UnclaimedTokens += isLocked ? 0 : total;
        }
        else
        {
            result.PoolStats[subject] = new StakeStats
            {
                TotalStaked = total,
                TotalPortfolio = total,
                UnclaimedTokens = isLocked ? 0 : total
            };
        }

        result.TotalStats.TotalStaked += total;
        result.TotalStats.TotalPortfolio += total;
        result.TotalStats.UnclaimedTokens += isLocked ? 0 : total;
    });

    return Results.Ok(result);
})
.WithName("GetStakeSummaryByStakeKeys")
.WithOpenApi();

app.MapPost("/stake/requests/", async (
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    [FromBody] List<string> addresses,
    [FromQuery] int page = 1,
    [FromQuery] int limit = 10
) =>
{
    using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

    int skip = (page - 1) * limit;

    List<StakeRequestByAddress> pagedData = await dbContext.StakeRequestByAddresses
        .Where(s => addresses.Contains(s.Address))
        .OrderByDescending(s => s.Slot)
        .Skip(skip)
        .Take(limit)
        .ToListAsync();

    int totalCount = await dbContext.StakeRequestByAddresses
        .CountAsync(s => addresses.Contains(s.Address));

    Dictionary<ulong, long> slotData = pagedData
        .DistinctBy(s => s.Slot)
        .ToDictionary(
            s => s.Slot,
            s => CoinectaUtils.TimeFromSlot(CoinectaUtils.GetNetworkType(configuration), (long)s.Slot)
        );

    return Results.Ok(new { Total = totalCount, Data = pagedData, Extra = new { SlotData = slotData } });
})
.WithName("GetStakeRequestsByAddresses")
.WithOpenApi();

app.MapGet("/stake/requests/pending", async (
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    [FromQuery] int page = 1,
    [FromQuery] int limit = 10
) =>
{
    using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

    int skip = (page - 1) * limit;

    List<StakeRequestByAddress> result = await dbContext.StakeRequestByAddresses
        .Where(s => s.Status == StakeRequestStatus.Pending)
        .OrderBy(s => s.Slot)
        .Skip(skip)
        .Take(limit)
        .ToListAsync();


    return Results.Ok(result);
})
.WithName("GetStakeRequests")
.WithOpenApi();


app.MapPost("/stake/positions", async (IDbContextFactory<CoinectaDbContext> dbContextFactory, IConfiguration configuration, [FromBody] List<string> stakeKeys) =>
{
    if (stakeKeys.Count == 0)
    {
        return Results.BadRequest("No stake keys provided");
    }

    using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

    // Current Timestamp
    DateTimeOffset dto = new(DateTime.UtcNow);
    ulong currentTimestamp = (ulong)dto.ToUnixTimeMilliseconds();

    // Get Stake Positions
    List<StakePositionByStakeKey> stakePositions = await dbContext.StakePositionByStakeKeys.Where(s => stakeKeys.Contains(s.StakeKey)).ToListAsync();

    // Transaform Stake Positions
    var result = stakePositions.Select(sp =>
    {
        // Remove NFT
        sp.Amount.MultiAsset.Remove(configuration["CoinectaStakeMintingPolicyId"]!);

        double interest = sp.Interest.Numerator / (double)sp.Interest.Denominator;
        string? policyId = sp.Amount.MultiAsset.Keys.FirstOrDefault();
        Dictionary<string, ulong> asset = sp.Amount.MultiAsset[policyId!];
        string assetName = asset.Keys.FirstOrDefault()!;
        string subject = policyId + assetName;
        ulong total = asset.Values.FirstOrDefault();
        ulong initial = (ulong)(total / (1 + interest));
        ulong bonus = total - initial;
        DateTimeOffset unlockDate = DateTimeOffset.FromUnixTimeMilliseconds((long)sp.LockTime);

        return new
        {
            Subject = subject,
            Total = total,
            UnlockDate = unlockDate,
            Initial = initial,
            Bonus = bonus,
            Interest = interest,
            sp.TxHash,
            sp.TxIndex,
            sp.StakeKey,
        };
    }).OrderByDescending(sp => sp.UnlockDate).ToList();

    return Results.Ok(result);
})
.WithName("GetStakePositionsByStakeKeys")
.WithOpenApi();

app.MapGet("/stake/stats", async (
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    [FromQuery] ulong? slot) =>
{
    using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

    IQueryable<StakePositionByStakeKey> stakePositionsQuery = dbContext.StakePositionByStakeKeys
        .AsNoTracking();

    if (slot.HasValue)
    {
        stakePositionsQuery = stakePositionsQuery.Where(s => s.Slot <= slot);
    }

    RationalEqualityComparer rationalEqualityComparer = new();

    var stakePositions = await stakePositionsQuery.GroupBy(s => new { s.TxHash, s.TxIndex })
        .Where(g => g.Count() < 2)
        .Select(g => new
        {
            g.First().Interest,
            LockUntil = g.First().LockTime,
            LockedAsset = g.First().StakePosition.Metadata.Data["locked_assets"],
            Expiration = g.First().StakePosition.Metadata.Data["name"].Substring(g.First().StakePosition.Metadata.Data["name"].LastIndexOf('-') + 1).Trim()
        })
        .ToListAsync();

    slot ??= await dbContext.Blocks.OrderByDescending(b => b.Slot).Select(b => b.Slot).FirstOrDefaultAsync();

    long slotTime = SlotUtility.GetPosixTimeSecondsFromSlot(
        CoinectaUtils.SlotUtilityFromNetwork(CoinectaUtils.GetNetworkType(configuration)),
        (long)slot) * 1000;


    var groupedByAsset = stakePositions
        .Select(sp =>
        {
            string[] lockedAssets = sp.LockedAsset!.Trim('[', ']').Trim('(', ')').Split(',');
            LockedAsset asset = new()
            {
                PolicyId = lockedAssets[0],
                AssetName = Encoding.UTF8.GetString(Convert.FromHexString(lockedAssets[1].Trim())),
                Amount = ulong.Parse(lockedAssets[2])
            };

            return new
            {
                sp.Interest,
                Asset = asset,
                sp.LockUntil,
                sp.Expiration
            };
        })
        .GroupBy(sp => new { sp.Asset.AssetName });

    List<PoolStats> groupedByInterest = groupedByAsset
        .Select(g =>
        {
            var groupedByInterest = g.GroupBy(sp => sp.Interest, rationalEqualityComparer).ToList();
            Dictionary<decimal, int> nftsByInterest = groupedByInterest.ToDictionary(
                g => (decimal)g.Key.Numerator / g.Key.Denominator,
                g => g.Count()
            );

            Dictionary<decimal, ulong> rewardsByInterest = groupedByInterest.ToDictionary(
                g => (decimal)g.Key.Numerator / g.Key.Denominator,
                g =>
                {
                    Rational amount = new(g.Aggregate(0UL, (acc, sp) => acc + sp.Asset.Amount), 1);
                    Rational interest = new(g.Key.Denominator, Denominator: g.Key.Numerator + g.Key.Denominator);
                    Rational originalStake = interest * amount;
                    ulong originalStakeAmount = originalStake.Numerator / originalStake.Denominator;
                    ulong amountWithStake = amount.Numerator;
                    return amountWithStake - originalStakeAmount;
                }
            );

            Dictionary<decimal, StakeData> stakeStatsByInterest = groupedByInterest.ToDictionary(
                g => (decimal)g.Key.Numerator / g.Key.Denominator,
                g =>
                {
                    // Total
                    Rational amount = new(g.Aggregate(0UL, (acc, sp) => acc + sp.Asset.Amount), 1);
                    Rational interest = new(g.Key.Denominator, Denominator: g.Key.Numerator + g.Key.Denominator);
                    Rational originalStakeTotal = interest * amount;
                    ulong totalAmount = originalStakeTotal.Numerator / originalStakeTotal.Denominator;

                    // Locked
                    Rational lockedAmount = new(g.Where(sp => sp.LockUntil > (ulong)slotTime).Aggregate(0UL, (acc, sp) => acc + sp.Asset.Amount), 1);
                    Rational originalLockedStakeTotal = interest * lockedAmount;
                    ulong totalLockedAmount = originalLockedStakeTotal.Numerator / originalLockedStakeTotal.Denominator;

                    // Unclaimed
                    ulong unclaimed = totalAmount - totalLockedAmount;

                    return new StakeData()
                    {
                        Total = totalAmount,
                        Locked = totalLockedAmount,
                        Unclaimed = unclaimed
                    };
                }
            );

            return new PoolStats()
            {
                AssetName = g.Key.AssetName,
                NftsByInterest = nftsByInterest,
                RewardsByInterest = rewardsByInterest,
                StakeDataByInterest = stakeStatsByInterest
            };
        })
        .ToList();

    List<PoolStats> groupedByExpiration = groupedByAsset
        .Select(g =>
        {
            var groupedByExpiration = g.GroupBy(sp => sp.Expiration).ToList();
            Dictionary<string, int> nftsByExpiration = groupedByExpiration.ToDictionary(
                g => g.Key,
                g => g.Count()
            );

            Dictionary<string, ulong> rewardsByExpiration = groupedByExpiration.ToDictionary(
                g => g.Key,
                g =>
                {
                    return g.GroupBy(sp => sp.Interest, rationalEqualityComparer).ToDictionary(
                        g => (decimal)g.Key.Numerator / g.Key.Denominator,
                        g =>
                        {
                            Rational amount = new(g.Aggregate(0UL, (acc, sp) => acc + sp.Asset.Amount), 1);
                            Rational interest = new(g.Key.Denominator, Denominator: g.Key.Numerator + g.Key.Denominator);
                            Rational originalStake = interest * amount;
                            ulong originalStakeAmount = originalStake.Numerator / originalStake.Denominator;
                            ulong amountWithStake = amount.Numerator;
                            return amountWithStake - originalStakeAmount;
                        }
                    ).Select(g => g.Value).Aggregate(0UL, (acc, rewards) => acc + rewards);
                }
            );

            Dictionary<string, StakeData> stakeDataByExpiration = groupedByExpiration.ToDictionary(
                g => g.Key,
                g =>
                {
                    Dictionary<decimal, StakeData> groupedByInterest = g.GroupBy(sp => sp.Interest, rationalEqualityComparer).ToDictionary(
                        g => (decimal)g.Key.Numerator / g.Key.Denominator,
                        g =>
                        {
                            // Total
                            Rational amount = new(g.Aggregate(0UL, (acc, sp) => acc + sp.Asset.Amount), 1);
                            Rational interest = new(g.Key.Denominator, Denominator: g.Key.Numerator + g.Key.Denominator);
                            Rational originalStakeTotal = interest * amount;
                            ulong totalAmount = originalStakeTotal.Numerator / originalStakeTotal.Denominator;

                            // Locked
                            Rational lockedAmount = new(g.Where(sp => sp.LockUntil > (ulong)slotTime).Aggregate(0UL, (acc, sp) => acc + sp.Asset.Amount), 1);
                            Rational originalLockedStakeTotal = interest * lockedAmount;
                            ulong totalLockedAmount = originalLockedStakeTotal.Numerator / originalLockedStakeTotal.Denominator;

                            // Unclaimed
                            ulong unclaimed = totalAmount - totalLockedAmount;

                            return new StakeData()
                            {
                                Total = totalAmount,
                                Locked = totalLockedAmount,
                                Unclaimed = unclaimed
                            };
                        }
                    );

                    return new StakeData()
                    {
                        Total = groupedByInterest.Select(g => g.Value.Total).Aggregate(0UL, (acc, total) => acc + total),
                        Locked = groupedByInterest.Select(g => g.Value.Locked).Aggregate(0UL, (acc, total) => acc + total),
                        Unclaimed = groupedByInterest.Select(g => g.Value.Unclaimed).Aggregate(0UL, (acc, total) => acc + total)
                    };
                }
            );

            return new PoolStats()
            {
                AssetName = g.Key.AssetName,
                NftsByExpiration = nftsByExpiration,
                RewardsByExpiration = rewardsByExpiration,
                StakeDataByExpiration = stakeDataByExpiration
            };
        })
        .ToList();

    List<PoolStats> result = groupedByInterest.GroupJoin(
        groupedByExpiration,
        gbi => gbi.AssetName,
        gbe => gbe.AssetName,
        (gbi, gbe) =>
        {
            PoolStats? expirationStats = gbe.FirstOrDefault();
            gbi.NftsByExpiration = expirationStats?.NftsByExpiration;
            gbi.RewardsByExpiration = expirationStats?.RewardsByExpiration;
            gbi.StakeDataByExpiration = expirationStats?.StakeDataByExpiration;
            return gbi;
        }
    ).ToList();

    return Results.Ok(result);
})
.WithName("GetStakePositionsSnapshot")
.WithOpenApi();


app.MapPost("/stake/snapshot", async (
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    ulong? slot, [FromBody] List<string>? addresses, int? offset, int? limit) =>
{
    using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();
    string stakeKeyPrefix = configuration["StakeKeyPrefix"]!;

    IQueryable<NftByAddress> nftsByAddressQuery = dbContext.NftsByAddress
        .AsNoTracking();

    IQueryable<StakePositionByStakeKey> stakePositionByStakeKeysQuery = dbContext.StakePositionByStakeKeys
        .AsNoTracking();

    var querySlot = 0UL;
    if (slot.HasValue)
    {
        querySlot = slot.Value;
    }
    else
    {
        querySlot = await dbContext.Blocks.OrderByDescending(b => b.Slot).Select(b => b.Slot).FirstOrDefaultAsync();
    }

    var stakePositionsByAddress = await nftsByAddressQuery
        .Where(n => n.Slot <= querySlot)
        .GroupBy(n => new { n.TxHash, n.OutputIndex, n.PolicyId, n.AssetName })
        .Where(g => g.Count() < 2)
        .Select(g => new
        {
            Key = string.Concat(g.First().PolicyId, g.First().AssetName.Substring(stakeKeyPrefix.Length)),
            g.First().Address
        })
        .Join(dbContext.StakePositionByStakeKeys, n => n.Key, s => s.StakeKey, (n, s) => new
        {
            n.Address,
            s.Interest,
            Amount = s.Amount.MultiAsset.Values.Last().Values.First()
        })
        .GroupBy(s => s.Address)
        .ToListAsync();

    var result = stakePositionsByAddress
        .Select(sp =>
        {
            ulong totalStake = sp.Select(s => s.Amount).Aggregate(0UL, (acc, stake) => acc + stake);

            return new
            {
                Address = sp.Key,
                UniqueNfts = sp.Count(),
                TotalStake = totalStake,
                CummulativeWeight = CoinectaUtils.CalculateTotalWeight(totalStake)
            };
        })
        .ToList();

    var totalStake = result.Select(r => r.TotalStake).Aggregate(0UL, (acc, stake) => acc + stake);
    var totalStakers = result.Count;
    var totalCummulativeWeight = result.Select(r => r.CummulativeWeight).Aggregate(0UL, (acc, weight) => acc + (ulong)weight);

    if (addresses is not null && addresses.Count > 0)
    {
        result = result.Where(r => addresses.Contains(r.Address)).ToList();
    }

    if (offset.HasValue && limit.HasValue)
    {
        result = result.Skip(offset.Value).Take(limit.Value).ToList();
    }

    return Results.Ok(new
    {
        Data = result,
        TotalStakers = totalStakers,
        TotalStake = totalStake,
        TotalCummulativeWeight = totalCummulativeWeight
    });
})
.WithName("GetAllStakeSnapshotByAddress")
.WithOpenApi();

app.MapPost("/transaction/stake/add", async (
    TransactionBuildingService txBuildingService,
    ILogger<Program> logger,
    [FromBody] AddStakeRequest request
) =>
{
    try
    {
        string result = await txBuildingService.AddStakeAsync(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error adding stake transaction");
        return Results.BadRequest(ex.Message);
    }
})
.WithName("AddStakeTransaction")
.WithOpenApi();

app.MapPost("/transaction/finalize", (
    TransactionBuildingService txBuildingService,
    ILogger<Program> logger,
    [FromBody] FinalizeTransactionRequest request
) =>
{
    try
    {
        string result = TransactionBuildingService.FinalizeTx(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error canceling stake transaction");
        return Results.BadRequest(ex.Message);
    }
})
.WithName("FinalizeTransaction")
.WithOpenApi();

app.MapPost("/transaction/stake/cancel", async (
    TransactionBuildingService txBuildingService,
    ILogger<Program> logger,
    [FromBody] CancelStakeRequest request
) =>
{
    try
    {
        string result = await txBuildingService.CancelStakeAsync(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error canceling stake transaction");
        return Results.BadRequest(ex.Message);
    }
})
.WithName("CancelStakeTransaction")
.WithOpenApi();

app.MapPost("/transaction/stake/claim", async (
    TransactionBuildingService txBuildingService,
    ILogger<Program> logger,
    [FromBody] ClaimStakeRequest request
) =>
{
    try
    {
        string result = await txBuildingService.ClaimStakeAsync(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error claiming stake transaction");
        return Results.BadRequest(ex.Message);
    }
})
.WithName("ClaimStakeTransaction")
.WithOpenApi();

app.MapPost("/transaction/stake/execute", async (
    TransactionBuildingService txBuildingService,
    ILogger<Program> logger,
    [FromBody] ExecuteStakeRequest request
) =>
{
    try
    {
        string result = await txBuildingService.ExecuteStakeAsync(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error executing stake transaction");
        return Results.BadRequest(ex.Message);
    }
})
.WithName("ExecuteStakeTransaction")
.WithOpenApi();

app.MapGet("/transaction/utxos/{address}", async (IDbContextFactory<CoinectaDbContext> dbContextFactory, [FromRoute] string address) =>
{
    using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

    if (string.IsNullOrEmpty(address))
    {
        return Results.BadRequest("Address is required");
    }

    List<UtxoByAddress> result = await dbContext.UtxosByAddress
        .Where(u => u.Address == address)
        .GroupBy(u => new { u.TxHash, u.TxIndex }) // Group by both TxHash and TxIndex
        .Where(g => g.Count() < 2)
        .Select(g => g.First())
        .ToListAsync();

    return Results.Ok(result);
})
.WithName("GetAddressUtxos")
.WithOpenApi();

app.MapGet("/transaction/utxos/raw/{address}", async (string address) =>
{
    try
    {
        CardanoNodeClient client = new();
        await client.ConnectAsync(builder.Configuration["CardanoNodeSocketPath"]!, builder.Configuration.GetValue<uint>("CardanoNetworkMagic"));
        Cardano.Sync.Data.Models.Experimental.UtxosByAddress utxosByAddress = await client.GetUtxosByAddressAsync(address);
        List<string> result = utxosByAddress.Values.Select(u =>
            Convert.ToHexString(CBORObject.NewArray().Add(u.Key.Value.GetCBOR()).Add(u.Value.Value.GetCBOR()).EncodeToBytes()).ToLowerInvariant()).ToList();

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
})
.WithName("GetRawUtxosByAddress")
.WithOpenApi();

app.MapPost("/transaction/utxos/raw", async (List<string> addresses) =>
{
    CardanoNodeClient client = new();
    await client.ConnectAsync(builder.Configuration["CardanoNodeSocketPath"]!, builder.Configuration.GetValue<uint>("CardanoNetworkMagic"));

    List<string> result = [];

    foreach (string address in addresses.Distinct())
    {
        try
        {
            Cardano.Sync.Data.Models.Experimental.UtxosByAddress utxosByAddress = await client.GetUtxosByAddressAsync(address);
            List<string> rawUtxosByAddress = utxosByAddress.Values.Select(u =>
                Convert.ToHexString(CBORObject.NewArray().Add(u.Key.Value.GetCBOR()).Add(u.Value.Value.GetCBOR()).EncodeToBytes()).ToLowerInvariant()).ToList();
            result.AddRange(rawUtxosByAddress);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting utxos for address {address}: {ex.Message}");
        }
    }

    return Results.Ok(result);
})
.WithName("GetRawUtxosByAddresses")
.WithOpenApi();

app.MapPost("/transaction/utxos/raw/balance", (List<string> utxosCbor) =>
{
    var utxos = CoinectaUtils.ConvertUtxoListCbor(utxosCbor).ToList();
    var balance = utxos.AggregateAssets();

    return Results.Ok(balance);
})
.WithName("GetBalanceFromRawUtxos")
.WithOpenApi();

app.MapGet("/block/latest", async (IDbContextFactory<CoinectaDbContext> dbContextFactory) =>
{
    using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

    Cardano.Sync.Data.Models.Block? result = await dbContext.Blocks.OrderByDescending(b => b.Slot).FirstOrDefaultAsync();

    return Results.Ok(result);
})
.WithName("GetLatestBlock")
.WithOpenApi();

app.UseCors();

app.Run();