using System.Net.Http.Json;
using System.Text.Json;
using Cardano.Sync.Data.Models;
using CardanoSharp.Wallet;
using CardanoSharp.Wallet.CIPs.CIP2.Models;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Models.Derivations;
using CardanoSharp.Wallet.Models.Keys;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.TransactionBuilding;
using CardanoSharp.Wallet.Utilities;
using Coinecta.Catcher.Models;
using Coinecta.Data.Models.Api.Request;
using Coinecta.Data.Models.Reducers;
using Coinecta.Data.Utils;
using Coinecta.Data.Extensions;
using TransactionOutput = CardanoSharp.Wallet.Models.Transactions.TransactionOutput;

namespace Coinecta.Catcher;

public class Worker(
    ILogger<Worker> logger,
    IConfiguration configuration,
    JsonSerializerOptions jsonSerializerOptions,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;
    private CatcherState CatcherState { get; set; } = new();
    private HttpClient CoinectaApi => httpClientFactory.CreateClient("CoinectaApi");
    private HttpClient SubmitApi => httpClientFactory.CreateClient("SubmitApi");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        MnemonicService mnemonicService = new();
        Mnemonic mnemonic = mnemonicService.Restore(configuration["CatcherMnemonic"]!);
        PrivateKey rootKey = mnemonic.GetRootKey();

        // Derive down to our Account Node
        IAccountNodeDerivation accountNode = rootKey.Derive()
            .Derive(PurposeType.Shelley)
            .Derive(CoinType.Ada)
            .Derive(0);

        IIndexNodeDerivation paymentNode = accountNode
            .Derive(RoleType.ExternalChain)
            .Derive(0);

        IIndexNodeDerivation stakeNode = accountNode
            .Derive(RoleType.Staking)
            .Derive(0);

        // Set Catcher States
        CatcherState.CatcherAddress = AddressUtility.GetBaseAddress(paymentNode.PublicKey, stakeNode.PublicKey, CoinectaUtils.GetNetworkType(configuration));
        CatcherState.CatcherPublicKey = paymentNode.PublicKey;
        CatcherState.CatcherPrivateKey = paymentNode.PrivateKey;
        CatcherState.SubmitApiUrl = configuration["CardanoSubmitApiUrl"]!;
        CatcherState.CatcherCertificatePolicyId = configuration["CoinectaBatchingCertificatePolicyId"]!;
        CatcherState.CatcherCertificateAssetName = configuration["CoinectaBatchingCertificateAssetName"]!;

        int pollingInterval = configuration.GetValue<int>("CoinectaStakeRequestPollingInterval") * 1_000;

        while (!stoppingToken.IsCancellationRequested)
        {
            // Fetch Pending Requests
            List<StakeRequestByAddress> stakeRequests = await FetchStakeRequestsAsync();

            // Fetch current slot
            _logger.LogInformation("Fetching Current Slot...");
            Block? latestBlock = await FetchLatestBlockAsync();

            if (latestBlock is null)
            {
                _logger.LogError("Error while fetching latest block.");
                await Task.Delay(pollingInterval, stoppingToken);
                continue;
            }

            ulong currentSlot = latestBlock.Slot;

            // Remove expired requests
            CatcherState.PendingExecutionStakeRequests = CatcherState.PendingExecutionStakeRequests
               .Where(p => p.TTL > currentSlot)
               .ToList();

            List<string> pendingExecutionStakeRequestOutrefs = CatcherState.PendingExecutionStakeRequests
                .Select(p => p.OutRef)
                .ToList();

            // Get all stake requests that needs to be processed
            List<StakeRequestByAddress> pendingStakeRequests = stakeRequests
                .Where(s => !pendingExecutionStakeRequestOutrefs.Contains(s.TxHash + s.TxIndex))
                .ToList();

            if (pendingStakeRequests.Count == 0)
            {
                _logger.LogInformation("No Stake Requests to process.");
                _logger.LogInformation("Pending Execution Stake Requests: {pendingExecutionStakeRequestCount}", CatcherState.PendingExecutionStakeRequests.Count);
                await Task.Delay(pollingInterval, stoppingToken);
                continue;
            }

            await UpdateCurrentStakePoolsAsync();
            await UpdateCurrentCollateralUtxoAsync();
            await UpdateCurrentCertificateUtxoAsync();

            // execute all transactions
            int stakeRequestCount = pendingStakeRequests.Count;
            _logger.LogInformation("Processing {stakeRequestCount} Stake Requests.", stakeRequestCount);
            foreach (StakeRequestByAddress stakeRequest in pendingStakeRequests)
            {
                StakePoolByAddress? stakePool = CatcherState.CurrentStakePoolStates!
                    .FirstOrDefault(s => s.StakePool.PolicyId.SequenceEqual(stakeRequest.StakePoolProxy.PolicyId) &&
                        s.StakePool.Owner.KeyHash.SequenceEqual(Convert.FromHexString(configuration["CoinectaStakePoolOwnerPkh"]!)));

                if (stakePool is not null)
                {
                    try
                    {
                        await ProcessStakeRequestAsync(
                            stakeRequest,
                            stakePool,
                            currentSlot,
                            stoppingToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error while executing stake request.");
                    }
                }
            }

            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(pollingInterval, stoppingToken);
        }
    }

    private async Task UpdateCurrentStakePoolsAsync()
    {
        _logger.LogInformation("Fetching Stake Pools...");
        CatcherState.CurrentStakePoolStates = CatcherState.CurrentStakePoolStates ?? await FetchStakePoolsAsync();
    }

    private async Task UpdateCurrentCertificateUtxoAsync()
    {
        CatcherState.CurrentCertificateUtxoState = CatcherState.CurrentCertificateUtxoState ?? await GetUpdatedCertificateUtxoAsync();
    }

    private async Task<Utxo> GetUpdatedCertificateUtxoAsync()
    {
        _logger.LogInformation("Fetching Certificate Utxo...");
        List<UtxoByAddress> result = await FetchUtxosAsync();

        List<Utxo> utxos = CoinectaUtils.ConvertUtxosByAddressToUtxo(result);
        ITokenBundleBuilder catcherTokenBundle = TokenBundleBuilder.Create;
        catcherTokenBundle.AddToken(Convert.FromHexString(CatcherState.CatcherCertificatePolicyId), Convert.FromHexString(CatcherState.CatcherCertificateAssetName), 1);

        TransactionOutput batcherCertificateOutput = new()
        {
            Address = CatcherState.CatcherAddress.GetBytes(),
            Value = new TransactionOutputValue()
            {
                MultiAsset = catcherTokenBundle.Build()
            }
        };
        CoinSelection catcherCoinSelectionResult = CoinectaUtils.GetCoinSelection([batcherCertificateOutput], utxos, CatcherState.CatcherAddress.ToString(), limit: 1);
        return catcherCoinSelectionResult.SelectedUtxos.First();
    }

    private async Task UpdateCurrentCollateralUtxoAsync()
    {
        CatcherState.CurrentCollateralUtxoState = CatcherState.CurrentCollateralUtxoState ?? await GetUpdatedCollateralUtxoAsync();
    }

    private async Task<Utxo> GetUpdatedCollateralUtxoAsync()
    {
        _logger.LogInformation("Fetching Collateral Utxo...");
        List<UtxoByAddress> result = await FetchUtxosAsync();
        List<Utxo> utxos = CoinectaUtils.ConvertUtxosByAddressToUtxo(result);

        TransactionOutput collateralOutput = new()
        {
            Address = CatcherState.CatcherAddress.GetBytes(),
            Value = new TransactionOutputValue()
            {
                Coin = 5_000_000,
                MultiAsset = []
            }
        };
        CoinSelection catcherCollateralCoinSelectionResult = CoinectaUtils.GetCoinSelection([collateralOutput], utxos, CatcherState.CatcherAddress.ToString(), limit: 1);

        return catcherCollateralCoinSelectionResult.SelectedUtxos.First();
    }

    private async Task<List<StakeRequestByAddress>> FetchStakeRequestsAsync()
    {
        try
        {
            HttpResponseMessage response = await CoinectaApi.GetAsync("/stake/requests/pending?page=1&limit=50");
            if (response.IsSuccessStatusCode)
            {

                string jsonString = await response.Content.ReadAsStringAsync();
                List<StakeRequestByAddress>? stakeRequestsByAddress = JsonSerializer.Deserialize<List<StakeRequestByAddress>>(jsonString, jsonSerializerOptions);
                return stakeRequestsByAddress ?? [];
            }
            else
            {
                // Handle error response
                _logger.LogError("Error while fetching stake requests. Status Code: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while fetching stake requests");
        }
        return [];
    }

    private async Task<List<StakePoolByAddress>> FetchStakePoolsAsync()
    {
        try
        {
            string stakePoolOwnerAddress = configuration["CoinectaStakePoolOwnerAddress"]!;
            string stakePoolOwnerPkh = configuration["CoinectaStakePoolOwnerPkh"]!;
            HttpResponseMessage response = await CoinectaApi.GetAsync($"stake/pools/{stakePoolOwnerAddress}/{stakePoolOwnerPkh}");
            if (response.IsSuccessStatusCode)
            {

                string jsonString = await response.Content.ReadAsStringAsync();
                List<StakePoolByAddress>? stakePoolsByAddress = JsonSerializer.Deserialize<List<StakePoolByAddress>>(jsonString, jsonSerializerOptions);
                return stakePoolsByAddress ?? [];
            }
            else
            {
                // Handle error response
                _logger.LogError("Error while fetching stake pools. Status Code: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while fetching stake pools");
        }

        return [];
    }

    private async Task<List<UtxoByAddress>> FetchUtxosAsync()
    {
        try
        {
            HttpResponseMessage response = await CoinectaApi.GetAsync($"/transaction/utxos/{CatcherState.CatcherAddress}");

            if (response.IsSuccessStatusCode)
            {
                string jsonString = await response.Content.ReadAsStringAsync();
                List<UtxoByAddress>? utxosByAddress = JsonSerializer.Deserialize<List<UtxoByAddress>>(jsonString, jsonSerializerOptions);
                return utxosByAddress ?? [];
            }
            else
            {
                // Handle error response
                _logger.LogError("Error while fetching utxos. Status Code: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while fetching utxos");
        }

        return [];
    }

    private async Task<Block?> FetchLatestBlockAsync()
    {
        try
        {
            HttpResponseMessage response = await CoinectaApi.GetAsync("/block/latest");
            if (response.IsSuccessStatusCode)
            {
                string jsonString = await response.Content.ReadAsStringAsync();
                Block? block = System.Text.Json.JsonSerializer.Deserialize<Block>(jsonString, jsonSerializerOptions);
                return block;
            }
            else
            {
                // Handle error response
                _logger.LogError("Error while fetching latest block. Status Code: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while fetching latest block");
        }

        return null;
    }

    private async Task ProcessStakeRequestAsync(
        StakeRequestByAddress stakeRequest,
        StakePoolByAddress stakePool,
        ulong currentSlot,
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing Stake Request: {stakeRequest.TxHash}", stakeRequest.TxHash);
        ExecuteStakeRequest request = new()
        {
            StakePoolData = stakePool,
            StakeRequestData = stakeRequest,
            CollateralUtxo = CatcherState.CurrentCollateralUtxoState,
            CertificateUtxo = CatcherState.CurrentCertificateUtxoState,
        };

        JsonContent executePayload = JsonContent.Create(request, request.GetType(), null, jsonSerializerOptions);
        HttpResponseMessage executeResponse = await CoinectaApi.PostAsync("transaction/stake/execute", executePayload, stoppingToken);
        string executeResponseJson = await executeResponse.Content.ReadAsStringAsync(stoppingToken);
        string unsignedTxCbor = JsonSerializer.Deserialize<string>(executeResponseJson)!;
        Transaction tx = Convert.FromHexString(unsignedTxCbor).DeserializeTransaction();

        string signedTxCbor = Convert.ToHexString(tx.SignAndSerializeStakeExecuteTx(new()
        {
            VKey = CatcherState.CatcherPublicKey,
            SKey = CatcherState.CatcherPrivateKey
        }));

        // Submit the transaction
        // When there's an error submitting the transaction, we reset the state
        string? txHash = string.Empty;
        try
        {
            // Execute the POST request
            ByteArrayContent submitPayload = new(Convert.FromHexString(signedTxCbor));
            submitPayload.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/cbor");
            HttpResponseMessage submitTxResponse = await SubmitApi.PostAsync("api/submit/tx", submitPayload, stoppingToken);

            // Read and output the response content
            txHash = await submitTxResponse.Content.ReadFromJsonAsync<string>(stoppingToken);

            if (!submitTxResponse.IsSuccessStatusCode)
            {
                throw new Exception($"Error while submitting transaction. Status Code: {submitTxResponse.StatusCode}. Response: {txHash}");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while submitting transaction. Resetting state...");
            CatcherState.CurrentStakePoolStates = null;
            CatcherState.CurrentCertificateUtxoState = null;
            CatcherState.CurrentCollateralUtxoState = null;

            await UpdateCurrentStakePoolsAsync();
            await UpdateCurrentCertificateUtxoAsync();
            await UpdateCurrentCollateralUtxoAsync();
            return;
        }

        // Add to Pending Execution
        _logger.LogInformation("Updating states..");
        CatcherState.PendingExecutionStakeRequests.Add(new()
        {
            OutRef = stakeRequest.TxHash + stakeRequest.TxIndex,
            TTL = (ulong)tx.TransactionBody.ValidBefore!
        });

        // Update State
        CatcherState.CurrentCertificateUtxoState!.Balance.Lovelaces = tx.TransactionBody.TransactionOutputs[3].Value.Coin;
        CatcherState.CurrentCertificateUtxoState.TxHash = txHash!;
        CatcherState.CurrentCertificateUtxoState.TxIndex = 3;

        int index = CatcherState.CurrentStakePoolStates!.IndexOf(stakePool);
        StakePoolByAddress oldPool = CatcherState.CurrentStakePoolStates[index];

        Dictionary<string, Dictionary<string, ulong>> multiAsset = [];
        foreach (KeyValuePair<byte[], NativeAsset> asset in tx.TransactionBody.TransactionOutputs[0].Value.MultiAsset)
        {
            multiAsset.Add(Convert.ToHexString(asset.Key).ToLower(), []);
            asset.Value.Token.Keys.ToList().ForEach(k =>
                multiAsset[Convert.ToHexString(asset.Key).ToLower()].Add(Convert.ToHexString(k).ToLower(), (ulong)asset.Value.Token[k]));
        }

        StakePoolByAddress updatedPool = new()
        {
            Address = oldPool.Address,
            Slot = currentSlot,
            TxHash = txHash!,
            TxIndex = 0,
            Amount = new()
            {
                Coin = tx.TransactionBody.TransactionOutputs[0].Value.Coin,
                MultiAsset = multiAsset
            },
            StakePool = oldPool.StakePool,
            StakePoolJson = oldPool.StakePoolJson
        };

        CatcherState.CurrentStakePoolStates[index] = updatedPool;
        _logger.LogInformation("Tx Submitted: {txHash}", txHash);
    }
}
