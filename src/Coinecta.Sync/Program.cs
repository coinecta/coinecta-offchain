using Cardano.Sync;
using Cardano.Sync.Reducers;
using Coinecta.Data.Models;
using Coinecta.Sync.Reducer;

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


app.Run();
