using Coinecta.Distributor;
using Polly;
using Polly.Extensions.Http;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

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
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(msg => !msg.IsSuccessStatusCode)
    .WaitAndRetryAsync(8, retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(3, retryAttempt)),
        onRetry: (outcome, timespan, retryAttempt, context) =>
        {
            Console.WriteLine($"Retry {retryAttempt} after {timespan.TotalSeconds} seconds due to {outcome.Result?.StatusCode}");
        }
    )
);

IHost host = builder.Build();
host.Run();

