using Asp.Versioning;
using Asp.Versioning.Builder;
using Carter;
using Coinecta.API.Modules.V1;

namespace Coinecta.API.Modules;

public class TransactionModule(TransactionHandler transactionHandlerV1) : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        ApiVersionSet apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        RouteGroupBuilder group = app
            .MapGroup("api/v{version:apiVersion}/transaction")
            .CacheOutput()
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Transaction")
            .WithOpenApi();

        group.MapPost("/treasury/create", transactionHandlerV1.CreateTreasury)
            .MapToApiVersion(1)
            .WithName("CreateTreasury")
            .WithDescription("Lock a UTxO to the treasury validator");

        group.MapPost("/treasury/withdraw", transactionHandlerV1.TreasuryWithdraw)
            .MapToApiVersion(1)
            .WithName("TreasuryWithdraw")
            .WithDescription("Owner unlocks a UTxO from the treasury validator");

        group.MapPost("/finalize", transactionHandlerV1.Finalize)
            .MapToApiVersion(1)
            .WithName("FinalizeTransaction")
            .WithDescription("Attach tx witness to unsigned tx");
    }
}