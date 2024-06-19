using Microsoft.EntityFrameworkCore;
using Coinecta.Data;
using Coinecta.Sync.Reducers;
using Cardano.Sync.Reducers;
using Cardano.Sync;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Reducers
builder.Services.AddSingleton<IReducer, StakePoolByAddressReducer>();
builder.Services.AddSingleton<IReducer, StakeRequestByAddressReducer>();
builder.Services.AddSingleton<IReducer, StakePositionByStakeKeyReducer>();
builder.Services.AddSingleton<IReducer, NftByAddressReducer>();
builder.Services.AddSingleton<IReducer, UtxosByAddressReducer>();

builder.Services.AddCardanoIndexer<CoinectaDbContext>(builder.Configuration, 60);

var app = builder.Build();

app.MapGet("/", () => DateTimeOffset.UtcNow);

using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<CoinectaDbContext>();
dbContext.Database.Migrate();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();