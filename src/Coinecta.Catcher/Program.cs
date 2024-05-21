using System.Text.Json;
using Coinecta.Catcher;
using Coinecta.Data.Services;
using Coinecta.Models.Api;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddSingleton(new JsonSerializerOptions
{
    Converters = { new UlongToStringConverter() },
    PropertyNameCaseInsensitive = true
});

builder.Services.AddHttpClient("SubmitApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["CardanoSubmitApiUrl"]!);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    HttpClientHandler handler = new()
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    return handler;
});

builder.Services.AddHttpClient("CoinectaApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["CoinectaApiBaseUrl"]!);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    HttpClientHandler handler = new()
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    return handler;
});

var host = builder.Build();
host.Run();
