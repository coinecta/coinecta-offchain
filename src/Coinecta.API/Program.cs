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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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
        string assetNameAscii = Encoding.ASCII.GetString(Convert.FromHexString(asset.Keys.FirstOrDefault()!));
        ulong total = asset.Values.FirstOrDefault();

        if (result.PoolStats.TryGetValue(assetNameAscii, out StakeStats? value))
        {
            value.TotalStaked += total;
            value.TotalPortfolio += total;
            value.UnclaimedTokens += isLocked ? 0 : total;
        }
        else
        {
            result.PoolStats[assetNameAscii] = new StakeStats
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

    var pagedData = await dbContext.StakeRequestByAddresses
        .Where(s => addresses.Contains(s.Address))
        .OrderByDescending(s => s.Slot)
        .Skip(skip)
        .Take(limit)
        .ToListAsync();

    var totalCount = await dbContext.StakeRequestByAddresses
        .CountAsync(s => addresses.Contains(s.Address));

    var slotData = pagedData
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

    var result = await dbContext.StakeRequestByAddresses
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
        string assetNameAscii = Encoding.ASCII.GetString(Convert.FromHexString(asset.Keys.FirstOrDefault()!));
        ulong total = asset.Values.FirstOrDefault();
        ulong initial = (ulong)(total / (1 + interest));
        ulong bonus = total - initial;
        DateTimeOffset unlockDate = DateTimeOffset.FromUnixTimeMilliseconds((long)sp.LockTime);

        return new
        {
            Name = assetNameAscii,
            Total = total,
            UnlockDate = unlockDate,
            Initial = initial,
            Bonus = bonus,
            Interest = interest,
            sp.TxHash,
            sp.TxIndex
        };
    }).OrderByDescending(sp => sp.UnlockDate).ToList();

    return Results.Ok(result);
})
.WithName("GetStakePositionsByStakeKeys")
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
        string result = txBuildingService.FinalizeTx(request);
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

    var result = await dbContext.UtxosByAddress
        .Where(u => u.Address == address)
        .GroupBy(u => new { u.TxHash, u.TxIndex }) // Group by both TxHash and TxIndex
        .Where(g => g.Count() < 2)
        .Select(g => g.First())
        .ToListAsync();

    return Results.Ok(result);
})
.WithName("GetAddressUtxos")
.WithOpenApi();

app.MapGet("/block/latest", async (IDbContextFactory<CoinectaDbContext> dbContextFactory) =>
{
    using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

    var result = await dbContext.Blocks.OrderByDescending(b => b.Slot).FirstOrDefaultAsync();

    return Results.Ok(result);
})
.WithName("GetLatestBlock")
.WithOpenApi();

app.UseCors();

app.Run();