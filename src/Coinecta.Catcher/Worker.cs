using Cardano.Sync.Data.Models.Datums;
using CardanoSharp.Wallet;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models.Addresses;
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
using PallasDotnet;

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
        var mnemonic = mnemonicService.Restore(configuration["CatcherMnemonic"]!);
        var rootKey = mnemonic.GetRootKey();

        // Derive down to our Account Node
        var accountNode = rootKey.Derive()
            .Derive(PurposeType.Shelley)
            .Derive(CoinType.Ada)
            .Derive(0);

        // Derive our Staking Node on Index 0
        var stakingNode = accountNode
            .Derive(RoleType.Staking)
            .Derive(0);

        // Deriving our Payment Node
        //  note: We did not derive down to the index.
        var paymentNode = accountNode
            .Derive(RoleType.ExternalChain)
            .Derive(0);


        var submitApiUrl = "https://submitapi-preview-api-test-ad9e12.us1.demeter.run/api/submit/tx";

        CardanoSharp.Wallet.Models.Addresses.Address address = AddressUtility.GetBaseAddress(paymentNode.PublicKey, stakingNode.PublicKey, NetworkType.Preview);
        var publicc = paymentNode.PublicKey;
        var publicKey = new PublicKey(address.GetPublicKeyHash(), paymentNode.PublicKey.Chaincode);
        byte[] header =
        [
            // Example header, replace with your actual header bytes
            0x00, // First byte of the header
        ]; // Your 4-byte header, set this to your actual header value
        byte[] extendedPkh = new byte[32]; // New 32-byte array
        Array.Copy(header, 0, extendedPkh, 0, header.Length);
        Array.Copy(publicKey.Key, 0, extendedPkh, header.Length, publicKey.Key.Length);

        var pubKeyString = Convert.ToHexString(extendedPkh);

        while (!stoppingToken.IsCancellationRequested)
        {
            using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

            // Fetch Pending Requests
            List<Data.Models.Reducers.StakeRequestByAddress> stakeRequests = await dbContext.StakeRequestByAddresses
                .AsNoTracking()
                .ToListAsync(cancellationToken: stoppingToken);

            // Fetch updated stake pool if not yet set
            CatcherState.CurrentStakePoolStates = CatcherState.CurrentStakePoolStates ?? await dbContext.StakePoolByAddresses
                .AsNoTracking()
                .OrderByDescending(s => s.Slot)
                .ToListAsync(cancellationToken: stoppingToken);

            // Fetch current slot
            ulong currentSlot = await dbContext.Blocks
                .AsNoTracking()
                .Select(b => b.Slot).OrderByDescending(b => b)
                .FirstOrDefaultAsync(cancellationToken: stoppingToken);

            // Remove expired requests
            List<string> validPendingStakeRequests = CatcherState.PendingExecutionStakeRequests
                .Where(p => p.TTL > currentSlot)
                .Select(psr => psr.StakeRequestOutRef!.TxHash + psr.StakeRequestOutRef.Index.ToString())
                .ToList();

            // Get all stake requests that needs to be processed
            List<StakeRequestByAddress> pendingStakeRequests = stakeRequests
                .Where(s => s.Status == StakeRequestStatus.Pending)
                .Where(s => !validPendingStakeRequests.Any(p => p == s.TxHash + s.TxIndex.ToString()))
                .ToList();

            // Collateral Utxo
            CatcherState.CurrentCollateralUtxoState = CatcherState.CurrentCollateralUtxoState ?? new()
            {
                TxHash = "7e326a8a026d9bcfaee624c40e5574b9948bcd153037d241d433ca83ab233598",
                TxIndex = 0,
                OutputAddress = "addr_test1qpg007fw5caetatd8gxcyt6d08lzteh9afk5smfd9hr60l72udjv5rtpfksjl64zeay5f2gpj6st0tl8m400nq8hjp9suxaw6c",
                Balance = new()
                {
                    Lovelaces = 100_000_000
                }
            };

            // Certificate Utxo
            CatcherState.CurrentCertificateUtxoState = CatcherState.CurrentCertificateUtxoState ?? new()
            {
                TxHash = "4d2814ce2908cf5801a13bfa9172fa57a6984d8881e3d1cdf72abc9373eb2acf",
                TxIndex = 0,
                OutputAddress = "addr_test1qpg007fw5caetatd8gxcyt6d08lzteh9afk5smfd9hr60l72udjv5rtpfksjl64zeay5f2gpj6st0tl8m400nq8hjp9suxaw6c",
                Balance = new()
                {
                    Lovelaces = 1168010,
                    Assets = [new(){
                        PolicyId = configuration["CoinectaBatchingCertificatePolicyId"]!,
                        Name = configuration["CoinectaBatchingCertificateAssetName"]!,
                        Quantity = 1
                    }]
                }
            };

            // execute all transactions
            foreach (StakeRequestByAddress stakeRequest in pendingStakeRequests)
            {
                StakePoolByAddress? stakePool = CatcherState.CurrentStakePoolStates
                    .FirstOrDefault(s => s.StakePool.PolicyId.SequenceEqual(stakeRequest.StakePoolProxy.PolicyId) &&
                        s.StakePool.Owner.KeyHash.SequenceEqual(Convert.FromHexString(configuration["CoinectaStakePoolOwnerPkh"]!)));

                if (stakePool is not null)
                {
                    var request = new ExecuteStakeRequest
                    {
                        StakePoolData = stakePool,
                        StakeRequestData = stakeRequest,
                        CollateralUtxo = CatcherState.CurrentCollateralUtxoState,
                        CertificateUtxo = CatcherState.CurrentCertificateUtxoState,
                    };

                    string unsignedTxCbor = await txBuildingService.ExecuteStakeAsync(request);

                    Transaction tx = Convert.FromHexString(unsignedTxCbor).DeserializeTransaction();
                    TransactionWitnessSetBuilder witnessSetBuilder = new();
                    tx.TransactionBody.ValidBefore = (uint)currentSlot + 500;
                    var pkh = address.GetPublicKeyHash();
                    Array.Resize(ref pkh, 32);
                    witnessSetBuilder.AddVKeyWitness(new()
                    {
                        VKey = new PublicKey(paymentNode.PublicKey.Key, paymentNode.PublicKey.Chaincode),
                        SKey = new PrivateKey(paymentNode.PrivateKey.Key, paymentNode.PrivateKey.Chaincode)
                    });

                    if (tx.TransactionWitnessSet is null)
                    {
                        tx.TransactionWitnessSet = witnessSetBuilder.Build();
                    }
                    else
                    {
                        tx.TransactionWitnessSet.VKeyWitnesses = witnessSetBuilder.Build().VKeyWitnesses;
                    }

                    string signedTxCbor = Convert.ToHexString(tx.Serialize());
                    var content = new ByteArrayContent(Convert.FromHexString(signedTxCbor));
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/cbor");

                    // Execute the POST request
                    var response = await httpClient.PostAsync(submitApiUrl, content, stoppingToken);

                    // Read and output the response content
                    var responseContent = await response.Content.ReadAsStringAsync(stoppingToken);
                    Console.WriteLine(responseContent);

                    string txHash = await response.Content.ReadAsStringAsync(stoppingToken);

                    // Add to Pending Execution
                    CatcherState.PendingExecutionStakeRequests.ToList().Add(new()
                    {
                        StakeRequestOutRef = new()
                        {
                            TxHash = stakeRequest.TxHash,
                            Index = stakeRequest.TxIndex
                        },
                        TTL = currentSlot + 500
                    });

                    // Update State
                    CatcherState.CurrentCertificateUtxoState.Balance.Lovelaces = tx.TransactionBody.TransactionOutputs[3].Value.Coin;
                    CatcherState.CurrentCertificateUtxoState.TxHash = txHash;
                    CatcherState.CurrentCertificateUtxoState.TxIndex = 3;

                    var index = CatcherState.CurrentStakePoolStates.IndexOf(stakePool);
                    var oldPool = CatcherState.CurrentStakePoolStates[index];

                    var multiAsset = new Dictionary<string, Dictionary<string, ulong>>();
                    foreach (var asset in tx.TransactionBody.TransactionOutputs[0].Value.MultiAsset)
                    {
                        multiAsset.Add(Convert.ToHexString(asset.Key), []);
                        asset.Value.Token.Keys.ToList().ForEach(k =>
                            multiAsset[Convert.ToHexString(asset.Key)].Add(Convert.ToHexString(k), (ulong)asset.Value.Token[k]));
                    }

                    var updatedPool = new StakePoolByAddress()
                    {
                        Address = oldPool.Address,
                        Slot = currentSlot,
                        TxHash = txHash,
                        TxIndex = 0,
                        Amount = new()
                        {
                            Coin = tx.TransactionBody.TransactionOutputs[0].Value.Coin,
                            MultiAsset = multiAsset
                        },
                    };

                    CatcherState.CurrentStakePoolStates[index] = updatedPool;

                    _logger.LogInformation("Signed Transaction: {signedTxCbor}", txHash);
                    _logger.LogInformation("Signed Transaction: {signedTxCbor}", signedTxCbor);
                }
            }

            await Task.Delay(20_000, stoppingToken);
        }
    }
}
