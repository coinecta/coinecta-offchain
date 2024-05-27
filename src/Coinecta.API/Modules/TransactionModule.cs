using Asp.Versioning;
using Asp.Versioning.Builder;
using Carter;

namespace Coinecta.API.Modules.V1;

public class TransactionModule(TransactionHandler transactionHandlerV1) : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        ApiVersionSet apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        // RouteGroupBuilder group = app
        //     .MapGroup("/api/v{version:apiVersion}/transaction")
        //     .WithApiVersionSet(apiVersionSet)
        //     .WithTags("Transaction")
        //     .WithOpenApi();

        RouteGroupBuilder group = app
            .MapGroup("/api/transaction")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Transaction")
            .WithOpenApi();

        group.MapPost("stake/add", transactionHandlerV1.AddStakeAsync)
            .WithName("AddStakeTransaction")
            .WithOpenApi();

        group.MapPost("stake/cancel", transactionHandlerV1.CancelStakeAsync)
            .WithName("CancelStakeTransaction")
            .WithOpenApi();

        group.MapPost("stake/claim", transactionHandlerV1.ClaimStakeAsync)
            .WithName("ClaimStakeTransaction")
            .WithOpenApi();

        group.MapPost("stake/execute", transactionHandlerV1.ExecuteStakeAsync)
            .WithName("ExecuteStakeTransaction")
            .WithOpenApi();

        group.MapPost("finalize", transactionHandlerV1.FinalizeTransaction)
            .WithName("FinalizeTransaction")
            .WithOpenApi();


        group.MapGet("utxos/{address}", transactionHandlerV1.GetUtxosByAddressAsync)
            .WithName("GetAddressUtxos")
            .WithOpenApi();

        group.MapPost("history", transactionHandlerV1.GetTransactionHistoryByAddressesAsync)
            .WithName("GetTransactionHistoryByAddresses")
            .WithOpenApi();

        // app.MapGet("/transaction/utxos/raw/{address}", async (string address) =>
        // {
        //     try
        //     {
        //         CardanoNodeClient client = new();
        //         await client.ConnectAsync(builder.Configuration["CardanoNodeSocketPath"]!, builder.Configuration.GetValue<uint>("CardanoNetworkMagic"));
        //         Cardano.Sync.Data.Models.Experimental.UtxosByAddress utxosByAddress = await client.GetUtxosByAddressAsync(address);
        //         List<string> result = utxosByAddress.Values.Select(u =>
        //             Convert.ToHexString(CBORObject.NewArray().Add(u.Key.Value.GetCBOR()).Add(u.Value.Value.GetCBOR()).EncodeToBytes()).ToLowerInvariant()).ToList();

        //         return Results.Ok(result);
        //     }
        //     catch (Exception ex)
        //     {
        //         return Results.BadRequest(ex.Message);
        //     }
        // })
        // .WithName("GetRawUtxosByAddress")
        // .WithOpenApi();

        //     app.MapPost("/transaction/utxos/raw", async (List<string> addresses) =>
        //     {
        //         CardanoNodeClient client = new();
        //         await client.ConnectAsync(builder.Configuration["CardanoNodeSocketPath"]!, builder.Configuration.GetValue<uint>("CardanoNetworkMagic"));

        //         List<string> result = [];

        //         foreach (string address in addresses.Distinct())
        //         {
        //             try
        //             {
        //                 Cardano.Sync.Data.Models.Experimental.UtxosByAddress utxosByAddress = await client.GetUtxosByAddressAsync(address);
        //                 List<string> rawUtxosByAddress = utxosByAddress.Values.Select(u =>
        //                     Convert.ToHexString(CBORObject.NewArray().Add(u.Key.Value.GetCBOR()).Add(u.Value.Value.GetCBOR()).EncodeToBytes()).ToLowerInvariant()).ToList();
        //                 result.AddRange(rawUtxosByAddress);
        //             }
        //             catch (Exception ex)
        //             {
        //                 Console.WriteLine($"Error getting utxos for address {address}: {ex.Message}");
        //             }
        //         }

        //         return Results.Ok(result);
        //     })
        //     .WithName("GetRawUtxosByAddresses")
        //     .WithOpenApi();

        //     app.MapPost("/transaction/utxos/raw/balance", (List<string> utxosCbor) =>
        //     {
        //         var utxos = CoinectaUtils.ConvertUtxoListCbor(utxosCbor).ToList();
        //         var balance = utxos.AggregateAssets();

        //         return Results.Ok(balance);
        //     })
        //     .WithName("GetBalanceFromRawUtxos")
        //     .WithOpenApi();

        //     app.MapGet("/block/latest", async (IDbContextFactory<CoinectaDbContext> dbContextFactory) =>
        //     {
        //         using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

        //         Cardano.Sync.Data.Models.Block? result = await dbContext.Blocks.OrderByDescending(b => b.Slot).FirstOrDefaultAsync();

        //         return Results.Ok(result);
        //     })
        //     .WithName("GetLatestBlock")
        //     .WithOpenApi();

    }
}
