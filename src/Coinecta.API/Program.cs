using Coinecta.Data;
using Microsoft.EntityFrameworkCore;

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/stake/pools/{address}", (IDbContextFactory<CoinectaDbContext> dbContextFactory, string address) =>
{
    var dbContext = dbContextFactory.CreateDbContext();
    var stakePools = dbContext.StakePoolByAddresses.Where(s => s.Address == address).OrderByDescending(s => s.Slot).ToListAsync();
    return stakePools;
})
.WithName("GetLatestStakePoolsByAddress")
.WithOpenApi();

app.Run();