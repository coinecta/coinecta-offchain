using System.Text;
using Coinecta.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Coinecta.Data.Models.Reducers;
using Coinecta.Data.Models.Response;
using Coinecta.Data.Models.Api;
using Coinecta.Data.Models.Api.Request;
using Coinecta.Data.Services;
using Coinecta.Data.Utils;
using CardanoSharp.Wallet.Enums;
using Coinecta.Models.Api;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Utilities;
using Cardano.Sync.Data.Models.Datums;
using Cardano.Sync;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using PeterO.Cbor2;
using CardanoSharp.Wallet.CIPs.CIP2.Extensions;
using Coinecta.Data.Models;
using Asset = Coinecta.Data.Models.Api.Asset;
using System.Text.Json;
using Carter;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddScoped<TransactionBuildingService>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName!.Replace('.', '_'));
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new UlongToStringConverter());
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        builder =>
        {
            builder.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.

app.MapCarter();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors();

app.Run();