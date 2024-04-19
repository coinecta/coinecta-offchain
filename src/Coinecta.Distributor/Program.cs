using Coinecta.Distributor;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();


builder.Services.AddHttpClient("SubmitApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["CardanoSubmitApiUrl"]!);
});

IHost host = builder.Build();
host.Run();
