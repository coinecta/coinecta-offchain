using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Coinecta.Data.Models.Api.Request;
using Microsoft.Extensions.Configuration;

namespace Coinecta.Data.Services;

public class TxSubmitService(IHttpClientFactory httpClientFactory)
{
    private HttpClient SubmitTxClient => httpClientFactory.CreateClient("SubmitTxClient");

    public async Task<string> SubmitTxAsync(string txRaw)
    {
        ByteArrayContent content = new(Convert.FromHexString(txRaw));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/cbor");
        HttpResponseMessage response = await SubmitTxClient.PostAsync("/api/submit/tx", content);

        if(!response.IsSuccessStatusCode)
        {
            throw new Exception($"Tx submission failed: {await response.Content.ReadAsStringAsync()}");
        }

        string responseString = await response.Content.ReadFromJsonAsync<string>() ?? throw new Exception("Response not a valid TxId string.");

        return responseString;
    }
}