using Asp.Versioning;
using Asp.Versioning.Builder;
using Carter;

namespace Coinecta.API.Modules.V1;

public class StakeModule(StakeHandler stakeHandlerV1) : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        ApiVersionSet apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        // RouteGroupBuilder group = app
        //     .MapGroup("/api/v{version:apiVersion}/stake")
        //     .WithApiVersionSet(apiVersionSet)
        //     .WithTags("Staking")
        //     .WithOpenApi();

        RouteGroupBuilder group = app
            .MapGroup("/api/stake")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Staking")
            .WithOpenApi();

        group.MapGet("pool/{address}/{ownerPkh}/{policyId}/{assetName}", stakeHandlerV1.GetStakePoolAsync)
            .WithName("GetStakePool")
            .WithOpenApi();

        group.MapGet("pools/{address}/{ownerPkh}", stakeHandlerV1.GetStakePoolsAsync)
            .WithName("GetStakePools")
            .WithOpenApi();

        group.MapPost("summary", stakeHandlerV1.GetStakeSummaryByStakeKeysAsync)
            .WithName("GetStakeSummaryByStakeKeys")
            .WithOpenApi();

        group.MapPost("requests", stakeHandlerV1.GetStakeRequestsByAddressesAsync)
            .WithName("GetStakeRequestsByAddresses")
            .WithOpenApi();

        group.MapGet("requests/pending", stakeHandlerV1.GetStakeRequestsAsync)
            .WithName("GetStakeRequests")
            .WithOpenApi();

        group.MapPost("positions", stakeHandlerV1.GetStakePositionsByStakeKeysAsync)
            .WithName("GetStakePositionsByStakeKeys")
            .WithOpenApi();

        group.MapGet("stats", stakeHandlerV1.GetStakePositionsSnapshotAsync)
            .WithName("GetStakePositionsSnapshot")
            .WithOpenApi();

        group.MapPost("snapshot", stakeHandlerV1.GetAllStakeSnapshotByAddressAync)
            .WithName("GetAllStakeSnapshotByAddress")
            .WithOpenApi();

    }
}
