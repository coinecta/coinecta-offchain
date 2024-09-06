using Asp.Versioning;
using Carter;
using Coinecta.API.Modules.V1;
using Coinecta.Data.Models;
using Coinecta.Data.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<TransactionHandler>();
builder.Services.AddSingleton<TreasuryHandler>();
builder.Services.AddSingleton<MpfService>();
builder.Services.AddSingleton<S3Service>();
builder.Services.AddSingleton<TxSubmitService>();
builder.Services.AddCarter();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'V";
    options.SubstituteApiVersionInUrl = true;
});

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

builder.Services.AddHttpClient("MpfClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MpfUrl"]!);
});

builder.Services.AddHttpClient("SubmitTxClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["SubmitTxUrl"]!);
});

var app = builder.Build();

app.MapCarter();
app.UseSwagger();
app.UseSwaggerUI();

app.Run();
