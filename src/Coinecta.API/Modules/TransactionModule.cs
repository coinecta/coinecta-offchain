using Asp.Versioning;
using Asp.Versioning.Builder;
using Carter;

namespace Coinecta.API.Modules;

public class TransactionModule() : CarterModule
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

        // Listings
        group.MapGet("/hello", () => "Hello World")
            .MapToApiVersion(1)
            .WithName("GetListingsByAddress")
            .WithDescription("Get all listings by address");
    }
}