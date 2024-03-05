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
using Coinecta.Data;
using Coinecta.Data.Models.Api.Request;
using Coinecta.Data.Models.Reducers;
using Coinecta.Data.Services;
using Coinecta.Data.Utils;
using Microsoft.EntityFrameworkCore;

namespace Coinecta.Catcher;

public class Worker(
    ILogger<Worker> logger, IDbContextFactory<CoinectaDbContext>
    dbContextFactory, IConfiguration configuration,
    TransactionBuildingService txBuildingService,
    HttpClient httpClient) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;
    private CatcherState CatcherState { get; set; } = new();

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

        while (!stoppingToken.IsCancellationRequested)
        {
            using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

            // Fetch Pending Requests
            List<StakeRequestByAddress> stakeRequests = await dbContext.StakeRequestByAddresses
                .AsNoTracking()
                .Where(s => s.Status == StakeRequestStatus.Pending)
                .ToListAsync(cancellationToken: stoppingToken);

            // Fetch updated stake pool if not yet set
            await UpdateCurrentStakePoolsAsync(dbContext, stoppingToken);

            // Fetch current slot
            ulong currentSlot = await dbContext.Blocks
                .AsNoTracking()
                .Select(b => b.Slot).OrderByDescending(b => b)
                .FirstOrDefaultAsync(cancellationToken: stoppingToken);

            // Remove expired requests
            CatcherState.PendingExecutionStakeRequests = CatcherState.PendingExecutionStakeRequests
               .Where(p => p.TTL <= currentSlot)
               .ToList();

            // Get all stake requests that needs to be processed
            List<StakeRequestByAddress> pendingStakeRequests = stakeRequests
                .Where(s => !CatcherState.PendingExecutionStakeRequests
                .Any(p => p.StakeRequestOutRef!.TxHash + p.StakeRequestOutRef.TxHash == s.TxHash + s.TxIndex))
                .ToList();

            // Collateral Utxo
            await UpdateCurrentCollateralUtxoAsync(dbContext);

            // Certificate Utxo
            await UpdateCurrentCertificateUtxoAsync(dbContext);

            // execute all transactions
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
                            dbContext,
                            stoppingToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error while executing stake request. Skipping...");
                    }
                }
            }

            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(5_000, stoppingToken);
        }
    }

    private async Task UpdateCurrentStakePoolsAsync(CoinectaDbContext dbContext, CancellationToken stoppingToken)
    {
        CatcherState.CurrentStakePoolStates = CatcherState.CurrentStakePoolStates ?? await dbContext.StakePoolByAddresses
            .AsNoTracking()
            .OrderByDescending(s => s.Slot)
            .ToListAsync(cancellationToken: stoppingToken)
                ?? [];
    }

    private async Task UpdateCurrentCertificateUtxoAsync(CoinectaDbContext dbContext)
    {
        CatcherState.CurrentCertificateUtxoState = CatcherState.CurrentCertificateUtxoState ?? await GetUpdatedCertificateUtxoAsync(dbContext);
    }

    private async Task<Utxo> GetUpdatedCertificateUtxoAsync(CoinectaDbContext dbContext)
    {
        var result = await dbContext.UtxosByAddress
            .Where(u => u.Address == CatcherState.CatcherAddress.ToString())
            .GroupBy(u => new { u.TxHash, u.TxIndex }) // Group by both TxHash and TxIndex
            .Where(g => g.Count() < 2)
            .Select(g => g.First())
            .ToListAsync();

        var utxos = CoinectaUtils.ConvertUtxosByAddressToUtxo(result);
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


    private async Task UpdateCurrentCollateralUtxoAsync(CoinectaDbContext dbContext)
    {
        CatcherState.CurrentCollateralUtxoState = CatcherState.CurrentCollateralUtxoState ?? await GetUpdatedCollateralUtxoAsync(dbContext);
    }

    private async Task<Utxo> GetUpdatedCollateralUtxoAsync(CoinectaDbContext dbContext)
    {
        var result = await dbContext.UtxosByAddress
            .Where(u => u.Address == CatcherState.CatcherAddress.ToString())
            .GroupBy(u => new { u.TxHash, u.TxIndex }) // Group by both TxHash and TxIndex
            .Where(g => g.Count() < 2)
            .Select(g => g.First())
            .ToListAsync();

        var utxos = CoinectaUtils.ConvertUtxosByAddressToUtxo(result);

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

    private async Task ProcessStakeRequestAsync(
        StakeRequestByAddress stakeRequest,
        StakePoolByAddress stakePool,
        ulong currentSlot,
        CoinectaDbContext dbContext,
        CancellationToken stoppingToken)
    {
        ExecuteStakeRequest request = new()
        {
            StakePoolData = stakePool,
            StakeRequestData = stakeRequest,
            CollateralUtxo = CatcherState.CurrentCollateralUtxoState,
            CertificateUtxo = CatcherState.CurrentCertificateUtxoState,
        };

        string unsignedTxCbor = await txBuildingService.ExecuteStakeAsync(request);

        Transaction tx = Convert.FromHexString(unsignedTxCbor).DeserializeTransaction();
        TransactionWitnessSetBuilder witnessSetBuilder = new();

        witnessSetBuilder.AddVKeyWitness(new()
        {
            VKey = CatcherState.CatcherPublicKey,
            SKey = CatcherState.CatcherPrivateKey
        });

        tx.TransactionWitnessSet.VKeyWitnesses = witnessSetBuilder.Build().VKeyWitnesses;
        string signedTxCbor = Convert.ToHexString(tx.Serialize());
        string txHashDerived = Convert.ToHexString(HashUtility.Blake2b256(tx.TransactionBody.GetCBOR(null).EncodeToBytes())).ToLowerInvariant();

        // Submit the transaction
        // When there's an error submitting the transaction, we reset the state
        try
        {
            ByteArrayContent content = new(Convert.FromHexString(signedTxCbor));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/cbor");

            // Execute the POST request
            HttpResponseMessage response = await httpClient.PostAsync(CatcherState.SubmitApiUrl, content, stoppingToken);

            // Read and output the response content
            string responseContent = await response.Content.ReadAsStringAsync(stoppingToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error while submitting transaction. Status Code: {response.StatusCode}. Response: {responseContent}");
            }

            Console.WriteLine(responseContent);
            await response.Content.ReadAsStringAsync(stoppingToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while submitting transaction. Resetting state...");
            CatcherState.CurrentStakePoolStates = null;
            CatcherState.CurrentCertificateUtxoState = null;
            CatcherState.CurrentCollateralUtxoState = null;

            await UpdateCurrentStakePoolsAsync(dbContext, stoppingToken);
            await UpdateCurrentCertificateUtxoAsync(dbContext);
            await UpdateCurrentCollateralUtxoAsync(dbContext);
            return;
        }

        // Add to Pending Execution
        CatcherState.PendingExecutionStakeRequests.ToList().Add(new()
        {
            StakeRequestOutRef = new()
            {
                TxHash = stakeRequest.TxHash,
                Index = stakeRequest.TxIndex
            },
            TTL = (ulong)tx.TransactionBody.ValidBefore!
        });

        // Update State
        CatcherState.CurrentCertificateUtxoState!.Balance.Lovelaces = tx.TransactionBody.TransactionOutputs[3].Value.Coin;
        CatcherState.CurrentCertificateUtxoState.TxHash = txHashDerived;
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
            TxHash = txHashDerived,
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

        _logger.LogInformation("TxHash: {txHashDerived}", txHashDerived);
    }
}
