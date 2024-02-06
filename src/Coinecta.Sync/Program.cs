using Microsoft.EntityFrameworkCore;
using Coinecta.Data;
using Coinecta.Sync.Reducers;
using Coinecta.Sync.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<CoinectaDbContext>(options =>
{
    options
    .UseNpgsql(
        builder.Configuration
        .GetConnectionString("CoinectaContext"),
            x =>
            {
                x.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    builder.Configuration.GetConnectionString("CoinectaContextSchema")
                );
            }
        );
});

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Reducers
builder.Services.AddSingleton<IBlockReducer, BlockReducer>();
builder.Services.AddSingleton<ICoreReducer, TransactionOutputReducer>();
builder.Services.AddSingleton<IReducer, StakePoolByAddressReducer>();
builder.Services.AddSingleton<IReducer, StakeRequestByAddressReducer>();

builder.Services.AddHostedService<CardanoIndexWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
