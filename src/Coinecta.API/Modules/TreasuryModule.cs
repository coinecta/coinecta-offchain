using Asp.Versioning;
using Asp.Versioning.Builder;
using Carter;
using Coinecta.API.Modules.V1;

namespace Coinecta.API.Modules;

public class TreasuryModule(TreasuryHandler treasuryHandlerV1) : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        ApiVersionSet apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        RouteGroupBuilder group = app
            .MapGroup("api/v{version:apiVersion}/treasury")
            .CacheOutput()
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Treasury")
            .WithOpenApi();

        // Treasury
        group.MapPost("/trie/create", treasuryHandlerV1.CreateTrieAsync)
            .MapToApiVersion(1)
            .WithName("CreateTreasuryTrie")
            .WithDescription("Create a treasury trie from a dictionary of claim entries");
    }
}