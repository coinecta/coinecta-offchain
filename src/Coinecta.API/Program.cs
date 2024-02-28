using System.Text;
using System.Text.Unicode;
using Coinecta.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Coinecta.API.Models.Response;
using Coinecta.API.Models;
using Coinecta.API.Models.Request;
using Coinecta.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<CoinectaDbContext>(options =>
{
    options
    .UseNpgsql(
        builder.Configuration
        .GetConnectionString("CoinectaContext"),
            x =>
            {
                x.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    builder.Configuration.GetConnectionString("CoinectaContextSchema")
                );
            }
        );
});

builder.Services.AddScoped<TransactionBuildingService>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/stake/pools/{address}", (IDbContextFactory<CoinectaDbContext> dbContextFactory, string address) =>
{
    using var dbContext = dbContextFactory.CreateDbContext();
    var stakePools = dbContext.StakePoolByAddresses.Where(s => s.Address == address).OrderByDescending(s => s.Slot).ToListAsync();
    return stakePools;
})
.WithName("GetLatestStakePoolsByAddress")
.WithOpenApi();

app.MapPost("/stake/summary", async (IDbContextFactory<CoinectaDbContext> dbContextFactory, IConfiguration configuration, [FromBody] List<string> stakeKeys) =>
{
    if (stakeKeys.Count == 0)
    {
        return Results.BadRequest("No stake keys provided");
    }

    using var dbContext = dbContextFactory.CreateDbContext();

    // Current Timestamp
    DateTimeOffset dto = new(DateTime.UtcNow);
    ulong currentTimestamp = (ulong)dto.ToUnixTimeMilliseconds();

    // Get Stake Positions
    var stakePositions = await dbContext.StakePositionByStakeKeys.Where(s => stakeKeys.Contains(s.StakeKey)).ToListAsync();

    // Filter Stake Positions
    var lockedPositions = stakePositions.Where(sp => sp.LockTime > currentTimestamp);
    var unclaimedPositions = stakePositions.Where(sp => sp.LockTime <= currentTimestamp);

    // Transaform Stake Positions
    var result = new StakeSummaryResponse();

    stakePositions.ForEach(sp =>
    {
        // Remove NFT
        sp.Amount.MultiAsset.Remove(configuration["CoinectaStakeKeyPolicyId"]!);
        var isLocked = sp.LockTime > currentTimestamp;
        var policyId = sp.Amount.MultiAsset.Keys.FirstOrDefault();
        var asset = sp.Amount.MultiAsset[policyId!];
        var assetNameAscii = Encoding.ASCII.GetString(Convert.FromHexString(asset.Keys.FirstOrDefault()!));
        var total = asset.Values.FirstOrDefault();

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

app.MapPost("/stake/positions", async (IDbContextFactory<CoinectaDbContext> dbContextFactory, IConfiguration configuration, [FromBody] List<string> stakeKeys) =>
{
    if (stakeKeys.Count == 0)
    {
        return Results.BadRequest("No stake keys provided");
    }

    using var dbContext = dbContextFactory.CreateDbContext();

    // Current Timestamp
    DateTimeOffset dto = new(DateTime.UtcNow);
    ulong currentTimestamp = (ulong)dto.ToUnixTimeMilliseconds();

    // Get Stake Positions
    var stakePositions = await dbContext.StakePositionByStakeKeys.Where(s => stakeKeys.Contains(s.StakeKey)).ToListAsync();

    // Transaform Stake Positions
    var result = stakePositions.Select(sp =>
    {
        // Remove NFT
        sp.Amount.MultiAsset.Remove(configuration["CoinectaStakeKeyPolicyId"]!);

        double interest = sp.Interest.Numerator / (double)sp.Interest.Denominator;
        var policyId = sp.Amount.MultiAsset.Keys.FirstOrDefault();
        var asset = sp.Amount.MultiAsset[policyId!];
        var assetNameAscii = Encoding.ASCII.GetString(Convert.FromHexString(asset.Keys.FirstOrDefault()!));
        var total = asset.Values.FirstOrDefault();
        var initial = total / (1 + interest);
        var bonus = total - initial;
        var unlockDate = DateTimeOffset.FromUnixTimeMilliseconds((long)sp.LockTime);

        return new
        {
            Name = assetNameAscii,
            Total = total,
            UnlockDate = unlockDate,
            Initial = initial,
            Bonus = bonus,
            Interest = interest
        };
    });

    return Results.Ok(result);
})
.WithName("GetStakePositionsByStakeKeys")
.WithOpenApi();

app.MapPost("/transaction/stake/add", async (TransactionBuildingService txBuildingService, [FromBody] AddStakeRequest request) =>
{
    try
    {
        var result = await txBuildingService.AddStakeAsync(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
})
.WithName("AddStakeTransaction")
.WithOpenApi();

app.MapPost("/transaction/finalize", (TransactionBuildingService txBuildingService, [FromBody] FinalizeTransactionRequest request) =>
{
    try
    {
        var result = txBuildingService.FinalizeTx(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
})
.WithName("FinalizeTransaction")
.WithOpenApi();

app.MapPost("/transaction/stake/cancel", async (TransactionBuildingService txBuildingService, [FromBody] CancelStakeRequest request) =>
{
    try
    {
        var result = await txBuildingService.CancelStakeAsync(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
})
.WithName("CancelStakeTransaction")
.WithOpenApi();

app.UseCors();

app.Run();