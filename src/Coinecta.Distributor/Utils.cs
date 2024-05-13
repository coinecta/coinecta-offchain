using CardanoSharp.Wallet.CIPs.CIP2;
using CardanoSharp.Wallet.CIPs.CIP2.ChangeCreationStrategies;
using CardanoSharp.Wallet.CIPs.CIP2.Models;
using CardanoSharp.Wallet.Extensions;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Models.Addresses;
using CardanoSharp.Wallet.Models.Keys;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.TransactionBuilding;
using CardanoSharp.Wallet.Utilities;
using CardanoSharp.Wallet.CIPs.CIP2.Extensions;
using CardanoSharp.Wallet.Enums;

namespace Coinecta.Distributor;

public record OutputData(string Address, ulong Lovelace, List<Asset> Assets);

public static class Utils
{
    // Returns the serialized transaction, consumed utxos and change utxos
    public static (byte[], string, List<Utxo>, List<Utxo>) BuildTx(string changeAddress, PublicKey vkey, PrivateKey skey, List<OutputData> outputData, List<Utxo> utxos)
    {
        // Create a transaction output
        List<TransactionOutput> outputs = [];
        outputData.ForEach(output =>
        {
            ITransactionOutputBuilder txOutputBuilder = TransactionOutputBuilder.Create;
            TransactionOutputValue outputValue = new() { Coin = output.Lovelace, MultiAsset = ConvertBalanceAssetToNativeAsset(output.Assets) };

            txOutputBuilder.SetAddress(new Address(output.Address).GetBytes());
            txOutputBuilder.SetTransactionOutputValue(outputValue);

            var minLovelaceRequired = txOutputBuilder.Build().CalculateMinUtxoLovelace();
            var lovelaceAmount = Math.Max(minLovelaceRequired, output.Lovelace);

            outputValue.Coin = lovelaceAmount;
            txOutputBuilder.SetTransactionOutputValue(outputValue);

            outputs.Add(txOutputBuilder.Build());
        });

        CoinSelection coinSelectionResult = GetCoinSelection(outputs, utxos, changeAddress) ?? throw new Exception("Coin selection failed");

        // Build the transaction body
        ITransactionBodyBuilder txBodyBuilder = TransactionBodyBuilder.Create;

        coinSelectionResult.SelectedUtxos.ForEach(utxo => txBodyBuilder.AddInput(utxo));
        outputs.ForEach(output => txBodyBuilder.AddOutput(output));
        coinSelectionResult.ChangeOutputs.ForEach(changeOutput => txBodyBuilder.AddOutput(changeOutput));

        // Build the transaction
        ITransactionBuilder txBuilder = TransactionBuilder.Create;
        txBuilder.SetBody(txBodyBuilder);

        VKeyWitness vkeyWitness = new()
        {
            VKey = vkey,
            SKey = skey,
        };
        txBuilder.SetWitnesses(TransactionWitnessSetBuilder.Create.AddVKeyWitness(vkeyWitness));
        Transaction tx = txBuilder.Build();

        // Set fee
        uint fee = tx.CalculateAndSetFee();
        tx.TransactionBody.TransactionOutputs.Last().Value.Coin -= fee;
        string txHash = Convert.ToHexString(HashUtility.Blake2b256(tx.TransactionBody.GetCBOR(null).EncodeToBytes())).ToLowerInvariant();

        // Return all the outputs going back to the change address
        List<TransactionOutput> changeOutputs = tx.TransactionBody.TransactionOutputs.Where(output => new Address(output.Address).ToString() == changeAddress).ToList();
        List<Utxo> changeUtxos = changeOutputs.Select(output => new Utxo()
        {
            TxHash = txHash,
            TxIndex = (uint)tx.TransactionBody.TransactionOutputs.IndexOf(output),
            OutputAddress = changeAddress,
            OutputDatumOption = output.DatumOption,
            OutputScriptReference = output.ScriptReference,
            Balance = new Balance()
            {
                Lovelaces = output.Value.Coin,
                Assets = ConvertNativeAssetToBalanceAsset(output.Value.MultiAsset)
            }
        }).ToList();

        return (tx.Serialize(), txHash, utxos, changeUtxos);
    }

    public static CoinSelection? GetCoinSelection(
        IEnumerable<TransactionOutput> outputs,
        IEnumerable<Utxo> utxos, string changeAddress,
        ITokenBundleBuilder? mint = null,
        List<Utxo>? requiredUtxos = null,
        int limit = 20, ulong feeBuffer = 0uL)
    {
        RandomImproveStrategy coinSelectionStrategy = new();
        SingleTokenBundleStrategy changeCreationStrategy = new();
        CoinSelectionService coinSelectionService = new(coinSelectionStrategy, changeCreationStrategy);

        int retries = 0;

        while (retries < 100)
        {
            try
            {
                CoinSelection result = coinSelectionService.GetCoinSelection(outputs, utxos, changeAddress, mint, requiredUtxos, limit, feeBuffer);
                return result;
            }
            catch (Exception)
            {
                retries++;
            }
        }

        return null;
    }

    public static List<Asset> ConvertNativeAssetToBalanceAsset(Dictionary<byte[], NativeAsset> multiAsset)
    {
        if (multiAsset == null)
        {
            return [];
        }

        List<Asset> balanceAssets = [];
        multiAsset.Keys.ToList().ForEach((policyId) =>
        {
            string policyIdHex = Convert.ToHexString(policyId).ToLowerInvariant();
            NativeAsset asset = multiAsset[policyId];
            asset.Token.Keys.ToList().ForEach(assetName =>
            {
                string assetNameHex = Convert.ToHexString(assetName).ToLowerInvariant();
                ulong amount = (ulong)asset.Token[assetName];
                balanceAssets.Add(new Asset
                {
                    PolicyId = policyIdHex,
                    Name = assetNameHex,
                    Quantity = (long)amount
                });
            });
        });

        return balanceAssets;
    }

    public static Dictionary<byte[], NativeAsset> ConvertBalanceAssetToNativeAsset(List<Asset> balanceAssets)
    {
        Dictionary<byte[], NativeAsset> multiAsset = [];
        balanceAssets.ForEach(asset =>
        {
            byte[] policyId = Convert.FromHexString(asset.PolicyId);
            byte[] assetName = Convert.FromHexString(asset.Name);
            if (!multiAsset.TryGetValue(policyId, out NativeAsset? value))
            {
                value = new NativeAsset();
                multiAsset[policyId] = value;
            }

            value.Token[assetName] = asset.Quantity;
        });

        return multiAsset;
    }

    public static Utxo MapUtxoByAddressToUtxo(TransactionInput input, TransactionOutput output)
    {
        Utxo utxo = new()
        {
            Balance = new Balance(),
            TxHash = input.TransactionId.ToStringHex(),
            TxIndex = input.TransactionIndex,
            OutputAddress = new Address(output.Address).ToString()
        };
        utxo.Balance.Lovelaces = output.Value.Coin;
        if (output.Value.MultiAsset != null && output.Value.MultiAsset.Count > 0)
        {
            utxo.Balance.Assets = output.Value.MultiAsset
                .SelectMany(
                    x =>
                        x.Value.Token.Select(
                            y =>
                                new Asset()
                                {
                                    PolicyId = x.Key.ToStringHex(),
                                    Name = y.Key.ToStringHex(),
                                    Quantity = y.Value
                                }
                        )
                )
                .ToList();
        }
        return utxo;
    }

    public static NetworkType GetNetworkType(IConfiguration configuration)
    {
        return configuration.GetValue<int>("CardanoNetwork") switch
        {
            764824073 => NetworkType.Mainnet,
            1 => NetworkType.Preprod,
            2 => NetworkType.Preview,
            1177 => NetworkType.Preview,
            _ => throw new NotImplementedException()
        };
    }
}