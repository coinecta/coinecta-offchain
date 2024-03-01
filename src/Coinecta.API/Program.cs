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
using Coinecta.Data.Models.Reducers;
using CardanoSharp.Wallet.Utilities;

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
    List<Coinecta.Data.Models.Reducers.StakePoolByAddress> stakePools = await dbContext.StakePoolByAddresses.Where(s => s.Address == address).OrderByDescending(s => s.Slot).ToListAsync();
    return Results.Ok(
        stakePools
            .Where(sp => Convert.ToHexString(sp.StakePool.Owner.KeyHash).Equals(ownerPkh, StringComparison.InvariantCultureIgnoreCase))
            .Where(sp => sp.Amount.MultiAsset.ContainsKey(policyId) && sp.Amount.MultiAsset[policyId].ContainsKey(assetName))
            .First()
    );
})
.WithName("GetStakePool")
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
        .Skip(skip)
        .Take(limit)
        .ToListAsync();

    var totalCount = await dbContext.StakeRequestByAddresses
                                    .CountAsync(s => addresses.Contains(s.Address));

    return Results.Ok(new { Total = totalCount, Data = pagedData });
})
.WithName("GetStakeRequestsByAddresses")
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
    List<Coinecta.Data.Models.Reducers.StakePositionByStakeKey> stakePositions = await dbContext.StakePositionByStakeKeys.Where(s => stakeKeys.Contains(s.StakeKey)).ToListAsync();

    // Transaform Stake Positions
    var result = stakePositions.Select(sp =>
    {
        // Remove NFT
        sp.Amount.MultiAsset.Remove(configuration["CoinectaStakeKeyPolicyId"]!);

        double interest = sp.Interest.Numerator / (double)sp.Interest.Denominator;
        string? policyId = sp.Amount.MultiAsset.Keys.FirstOrDefault();
        Dictionary<string, ulong> asset = sp.Amount.MultiAsset[policyId!];
        string assetNameAscii = Encoding.ASCII.GetString(Convert.FromHexString(asset.Keys.FirstOrDefault()!));
        ulong total = asset.Values.FirstOrDefault();
        double initial = total / (1 + interest);
        double bonus = total - initial;
        DateTimeOffset unlockDate = DateTimeOffset.FromUnixTimeMilliseconds((long)sp.LockTime);

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
        string result = await txBuildingService.AddStakeAsync(request);
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
        string result = txBuildingService.FinalizeTx(request);
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
        string result = await txBuildingService.CancelStakeAsync(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
})
.WithName("CancelStakeTransaction")
.WithOpenApi();

app.MapPost("/transaction/stake/claim", async (TransactionBuildingService txBuildingService, [FromBody] ClaimStakeRequest request) =>
{
    try
    {
        string result = await txBuildingService.ClaimStakeAsync(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
})
.WithName("ClaimStakeTransaction")
.WithOpenApi();

app.MapPost("/transaction/stake/execute", async (TransactionBuildingService txBuildingService, [FromBody] ExecuteStakeRequest request) =>
{
    try
    {
        string result = await txBuildingService.ExecuteStakeAsync(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
})
.WithName("ExecuteStakeTransaction")
.WithOpenApi();

app.UseCors();

app.Run();