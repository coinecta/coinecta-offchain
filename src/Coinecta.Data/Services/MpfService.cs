using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Coinecta.Data.Models.Api.Request;
using Microsoft.Extensions.Configuration;

namespace Coinecta.Data.Services;

public class MpfService(IHttpClientFactory httpClientFactory)
{
    private HttpClient MpfClient => httpClientFactory.CreateClient("MpfClient");

    public async Task<string> CreateAsync(CreateMpfRequest request)
    {
        string jsonContent = JsonSerializer.Serialize(request).ToLowerInvariant();
        StringContent content = new(jsonContent, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await MpfClient.PostAsync("/mpf/create", content);
        response.EnsureSuccessStatusCode();
        string responseString = await response.Content.ReadFromJsonAsync<string>() ?? string.Empty;

        return responseString;
    }

    public async Task<string> GetProofAsync(MpfProofRequest request)
    {
        string jsonContent = JsonSerializer.Serialize(request).ToLowerInvariant();
        StringContent content = new(jsonContent, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await MpfClient.PostAsync("/mpf/proof", content);
        response.EnsureSuccessStatusCode();
        string responseString = await response.Content.ReadFromJsonAsync<string>() ?? string.Empty;

        return responseString;
    }
}