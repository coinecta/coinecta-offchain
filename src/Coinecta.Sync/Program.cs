using Cardano.Sync;
using Cardano.Sync.Reducers;
using Coinecta.Data.Models;
using Coinecta.Sync.Reducer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCardanoIndexer<CoinectaDbContext>(builder.Configuration);

builder.Services.AddSingleton<IReducer, VestingTreasuryReducer>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
} 

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
var context = services.GetRequiredService<CoinectaDbContext>();
if (context.Database.GetPendingMigrations().Any())
    context.Database.Migrate();

app.Run();
