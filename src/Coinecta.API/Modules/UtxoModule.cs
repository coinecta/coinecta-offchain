using Asp.Versioning;
using Asp.Versioning.Builder;
using Carter;
using Coinecta.Data;
using Microsoft.EntityFrameworkCore;

namespace Coinecta.API.Modules.V1;

public class UtxoModule(UtxoHandler utxoHandlerV1) : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        ApiVersionSet apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        RouteGroupBuilder group = app
            .MapGroup("utxo")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Utxo")
            .WithOpenApi();

        group.MapPost("refresh", utxoHandlerV1.UpdateUtxoTrackerAsync)
        .WithName("RefreshUtxoTrackerAsync")
        .WithOpenApi();
    }
}
