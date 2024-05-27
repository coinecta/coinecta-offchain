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

        RouteGroupBuilder group = app
            .MapGroup("transaction")
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

        group.MapPost("history", transactionHandlerV1.GetTransactionHistoryByAddressesAsync)
            .WithName("GetTransactionHistoryByAddresses")
            .WithOpenApi();

        group.MapGet("utxos/{address}", transactionHandlerV1.GetUtxosByAddressAsync)
            .WithName("GetAddressUtxos")
            .WithOpenApi();

        group.MapGet("utxos/raw/{address}", transactionHandlerV1.GetRawUtxosByAddressAsync)
            .WithName("GetRawUtxosByAddress")
            .WithOpenApi();

        group.MapPost("utxos/raw", transactionHandlerV1.GetRawUtxosByAddressesAsync)
            .WithName("GetRawUtxosByAddresses")
            .WithOpenApi();

        group.MapPost("utxos/raw/balance", transactionHandlerV1.GetBalanceFromRawUtxos)
            .WithName("GetBalanceFromRawUtxos")
            .WithOpenApi();
    }
}
