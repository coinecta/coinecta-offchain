using Asp.Versioning;
using Asp.Versioning.Builder;
using Carter;
using Coinecta.Data;
using Microsoft.EntityFrameworkCore;

namespace Coinecta.API.Modules.V1;

public class BlockModule() : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        ApiVersionSet apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        RouteGroupBuilder group = app
            .MapGroup("block")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Block")
            .WithOpenApi();

        group.MapGet("latest", async (IDbContextFactory<CoinectaDbContext> dbContextFactory) =>
        {
            using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

            Cardano.Sync.Data.Models.Block? result = await dbContext.Blocks.OrderByDescending(b => b.Slot).FirstOrDefaultAsync();

            return Results.Ok(result);
        })
        .WithName("GetLatestBlock")
        .WithOpenApi();
    }
}
