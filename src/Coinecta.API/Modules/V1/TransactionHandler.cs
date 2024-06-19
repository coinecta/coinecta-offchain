using System.Text.Json;
using Cardano.Sync;
using CardanoSharp.Wallet.CIPs.CIP2.Extensions;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using Coinecta.Data;
using Coinecta.Data.Models.Api;
using Coinecta.Data.Models.Api.Request;
using Coinecta.Data.Models.Reducers;
using Coinecta.Data.Services;
using Coinecta.Data.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PeterO.Cbor2;

namespace Coinecta.API.Modules.V1;

public class TransactionHandler(
    TransactionBuildingService txBuildingService,
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    ILogger<TransactionHandler> logger
)
{
    public async Task<IResult> AddStakeAsync(AddStakeRequest request)
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
    }

    public async Task<IResult> CancelStakeAsync(CancelStakeRequest request)
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
    }

    public async Task<IResult> ClaimStakeAsync(ClaimStakeRequest request)
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
    }

    public async Task<IResult> ExecuteStakeAsync(ExecuteStakeRequest request)
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
    }

    public IResult FinalizeTransaction(FinalizeTransactionRequest request)
    {
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
        }
    }

    public async Task<IResult> GetUtxosByAddressAsync(string address)
    {
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

        if (string.IsNullOrWhiteSpace(address))
        {
            return Results.BadRequest("Invalid address");
        }

        List<string>? result = await dbContext.UtxosByAddress
            .Where(u => u.Address == address)
            .Select(u => u.UtxoListCbor)
            .FirstOrDefaultAsync();

        return Results.Ok(result);
    }

    public async Task<IResult> GetTransactionHistoryByAddressesAsync(List<string> addresses, [FromQuery] int offset = 0, [FromQuery] int limit = 10)
    {
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

        var txHistoryRaw = await dbContext.Database
            .SqlQuery<TransactionHistoryRaw>($@"
            SELECT * FROM coinecta.GetTransactionHistoryByAddress({addresses}, {offset}, {limit})
        ")
            .ToListAsync();

        var txHistory = txHistoryRaw.Select(t => new TransactionHistory()
        {
            Address = t.Address,
            TxType = t.TxType,
            Lovelace = t.Lovelace,
            Assets = t.Assets != null ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, ulong>>>(t.Assets) : null,
            TxHash = t.TxHash,
            TxIndex = t.OutputIndex,
            CreatedAt = CoinectaUtils.TimeFromSlot(CoinectaUtils.GetNetworkType(configuration), (long)t.Slot),
            LockDuration = t.LockDuration,
            UnlockTime = t.UnlockTime == 0 ? null : t.UnlockTime,
            StakeKey = t.StakeKey,
            TransferredToAddress = t.TransferredToAddress,
        }).ToList();

        return Results.Ok(new { Total = txHistoryRaw.FirstOrDefault()?.TotalCount ?? 0, Data = txHistory });
    }

    public async Task<IResult> GetRawUtxosByAddressAsync(string address)
    {
        try
        {
            CardanoNodeClient client = new();
            await client.ConnectAsync(configuration["CardanoNodeSocketPath"]!, configuration.GetValue<uint>("CardanoNetworkMagic"));
            Cardano.Sync.Data.Models.Experimental.UtxosByAddress utxosByAddress = await client.GetUtxosByAddressAsync(address);
            List<string> result = utxosByAddress.Values.Select(u =>
                Convert.ToHexString(CBORObject.NewArray().Add(u.Key.Value.GetCBOR()).Add(u.Value.Value.GetCBOR()).EncodeToBytes()).ToLowerInvariant()).ToList();

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    public async Task<IResult> GetRawUtxosByAddressesAsync(List<string> addresses)
    {
        CardanoNodeClient client = new();
        await client.ConnectAsync(configuration["CardanoNodeSocketPath"]!, configuration.GetValue<uint>("CardanoNetworkMagic"));

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
    }

    public IResult GetBalanceFromRawUtxos(List<string> utxosCbor)
    {
        var utxos = CoinectaUtils.ConvertUtxoListCbor(utxosCbor).ToList();
        var balance = utxos.AggregateAssets();

        return Results.Ok(balance);
    }

    public IResult Test()
    {
        return Results.Ok(DateTimeOffset.UtcNow);
    }

    public IResult TestAdd()
    {
        return Results.Ok(DateTimeOffset.UtcNow.AddMilliseconds(300_000));
    }
}