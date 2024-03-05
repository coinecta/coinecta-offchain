using Coinecta.Catcher;
using Coinecta.Data;
using Coinecta.Data.Services;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

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

builder.Services.AddHttpClient();
builder.Services.AddSingleton<TransactionBuildingService>();

var host = builder.Build();
host.Run();
