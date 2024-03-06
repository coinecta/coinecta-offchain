using PeterO.Cbor2;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Extensions.Models;
using Address = CardanoSharp.Wallet.Models.Addresses.Address;
using CardanoSharp.Wallet.TransactionBuilding;
using CardanoSharp.Wallet.Utilities;
using CardanoSharp.Wallet.Models.Transactions.TransactionWitness.PlutusScripts;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions.TransactionWitness;
using CardanoSharp.Wallet.Extensions.Models.Transactions.TransactionWitnesses;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.CIPs.CIP2.Models;
using CardanoSharp.Wallet.CIPs.CIP2;
using CardanoSharp.Wallet.CIPs.CIP2.ChangeCreationStrategies;
using System.Numerics;
using Coinecta.Data.Models;
using Microsoft.Extensions.Configuration;
using Coinecta.Data.Migrations;
using Coinecta.Data.Models.Reducers;
using UtxoByAddress = Coinecta.Data.Models.Reducers.UtxoByAddress;
using CardanoSharp.Wallet.Common;

namespace Coinecta.Data.Utils;

public static class CoinectaUtils
{
    public static IEnumerable<Utxo> ConvertUtxoListCbor(IEnumerable<string> utxoCbors)
    {
        return utxoCbors.Select(utxoCbor =>
        {
            CBORObject utxoCborObj = CBORObject.DecodeFromBytes(Convert.FromHexString(utxoCbor));
            return utxoCborObj.GetUtxo();
        }).ToList();
    }

    public static IEnumerable<TransactionOutput> ConvertTxOutputListCbor(IEnumerable<string> txOutputCbors)
    {
        return txOutputCbors.Select(txOutputCbor =>
        {
            CBORObject txOutputCborObj = CBORObject.DecodeFromBytes(Convert.FromHexString(txOutputCbor));
            return txOutputCborObj.GetTransactionOutput();
        }).ToList();
    }

    public static TransactionWitnessSet ConvertTxWitnessSetCbor(string txWitnessSetCbor)
    {
        CBORObject txWitnessSetCborObj = CBORObject.DecodeFromBytes(Convert.FromHexString(txWitnessSetCbor));
        return txWitnessSetCborObj.GetTransactionWitnessSet();
    }

    public static Address ConvertAddressCbor(string addressCbor)
    {
        return new Address(Convert.FromHexString(addressCbor));
    }

    public static Address ValidatorAddress(byte[] validatorScriptCbor, IConfiguration configuration)
    {
        PlutusV2Script plutusScript = PlutusV2ScriptBuilder.Create
        .SetScript(validatorScriptCbor)
        .Build();

        return ValidatorAddress(plutusScript, configuration);
    }

    public static NetworkType GetNetworkType(IConfiguration configuration)
    {
        return configuration.GetValue<NetworkType>("CardanoNetworkMagic");
    }

    public static Address ValidatorAddress(PlutusV2Script plutusScript, IConfiguration configuration)
    {
        return AddressUtility.GetEnterpriseScriptAddress(plutusScript, GetNetworkType(configuration));
    }

    public static TransactionOutput ConvertTxOutputCbor(string txOutputCbor)
    {
        CBORObject txOutputCborObj = CBORObject.DecodeFromBytes(Convert.FromHexString(txOutputCbor));
        return txOutputCborObj.GetTransactionOutput();
    }

    public static TransactionInput BuildTxInput(OutputReference outputRef, TransactionOutput output)
    {
        return new TransactionInput
        {
            TransactionId = Convert.FromHexString(outputRef.TxHash),
            TransactionIndex = (uint)outputRef.Index,
            Output = output
        };
    }

    public static TransactionInput GetStakePoolProxyScriptReferenceInput(IConfiguration configuration)
    {
        string txHash = configuration["CoinectaStakePoolProxyScriptReferenceTxHash"]!;
        string txIndex = configuration["CoinectaStakePoolProxyScriptReferenceTxIndex"]!;
        OutputReference txOutputRef = new()
        {
            TxHash = txHash,
            Index = uint.Parse(txIndex)
        };
        string txOutputCbor = configuration["CoinectaStakePoolProxyScriptReferenceOutputCbor"]!;
        TransactionOutput txOutput = ConvertTxOutputCbor(txOutputCbor);
        TransactionInput resolvedTxInput = BuildTxInput(txOutputRef, txOutput);

        return resolvedTxInput;
    }

    public static TransactionInput GetStakePoolValidatorScriptReferenceInput(IConfiguration configuration)
    {
        string txHash = configuration["CoinectaStakePoolValidatorScriptReferenceTxHash"]!;
        string txIndex = configuration["CoinectaStakePoolValidatorScriptReferenceTxIndex"]!;
        OutputReference txOutputRef = new()
        {
            TxHash = txHash,
            Index = uint.Parse(txIndex)
        };
        string txOutputCbor = configuration["CoinectaStakePoolValidatorScriptReferenceOutputCbor"]!;
        TransactionOutput txOutput = ConvertTxOutputCbor(txOutputCbor);
        TransactionInput resolvedTxInput = BuildTxInput(txOutputRef, txOutput);

        return resolvedTxInput;
    }

    public static TransactionInput GetTimeLockValidatorScriptReferenceInput(IConfiguration configuration)
    {
        string txHash = configuration["CoinectaTimeLockValidatorScriptReferenceTxHash"]!;
        string txIndex = configuration["CoinectaTimeLockValidatorScriptReferenceTxIndex"]!;
        OutputReference txOutputRef = new()
        {
            TxHash = txHash,
            Index = uint.Parse(txIndex)
        };
        string txOutputCbor = configuration["CoinectaTimeLockValidatorScriptReferenceOutputCbor"]!;
        TransactionOutput txOutput = ConvertTxOutputCbor(txOutputCbor);
        TransactionInput resolvedTxInput = BuildTxInput(txOutputRef, txOutput);

        return resolvedTxInput;
    }

    public static TransactionInput GetStakeMintingValidatorScriptReferenceInput(IConfiguration configuration)
    {
        string txHash = configuration["CoinectaStakeMintingValidatorReferenceTxHash"]!;
        string txIndex = configuration["CoinectaStakeMintingValidatorReferenceTxIndex"]!;
        OutputReference txOutputRef = new()
        {
            TxHash = txHash,
            Index = uint.Parse(txIndex)
        };
        string txOutputCbor = configuration["CoinectaStakeMintingValidatorReferenceOutputCbor"]!;
        TransactionOutput txOutput = ConvertTxOutputCbor(txOutputCbor);
        TransactionInput resolvedTxInput = BuildTxInput(txOutputRef, txOutput);

        return resolvedTxInput;
    }

    public static CoinSelection GetCoinSelection(
        IEnumerable<TransactionOutput> outputs,
        IEnumerable<Utxo> utxos, string changeAddress,
        ITokenBundleBuilder? mint = null,
        List<Utxo>? requiredUtxos = null,
        int limit = 20, ulong feeBuffer = 0uL)
    {
        LargestFirstStrategy coinSelectionStrategy = new();
        SingleTokenBundleStrategy changeCreationStrategy = new();
        CoinSelectionService coinSelectionService = new(coinSelectionStrategy, changeCreationStrategy);

        CoinSelection result = coinSelectionService
            .GetCoinSelection(outputs, utxos, changeAddress, mint, requiredUtxos, limit, feeBuffer);

        return result;
    }

    public static ITokenBundleBuilder GetTokenBundleFromAmount(Dictionary<string, Dictionary<string, ulong>> amount)
    {
        ITokenBundleBuilder multiAssetBuilder = TokenBundleBuilder.Create;
        amount.Keys.ToList().ForEach((policyId) =>
        {
            Dictionary<string, ulong> asset = amount[policyId];
            asset.Keys.ToList().ForEach((assetName) =>
            {
                byte[] policyIdBytes = Convert.FromHexString(policyId);
                byte[] assetNameBytes = Convert.FromHexString(assetName);

                multiAssetBuilder.AddToken(policyIdBytes, assetNameBytes, (long)asset[assetName]);
            });
        });

        return multiAssetBuilder;
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
            var asset = multiAsset[policyId];
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

    public static List<Utxo> ConvertUtxosByAddressToUtxo(List<UtxoByAddress> utxosByAddress)
    {
        List<Utxo> resolvedUtxos = utxosByAddress
            .Select(u =>
            {
                var resolvedOutput = ConvertTxOutputCbor(Convert.ToHexString(u.TxOutCbor!));
                return new Utxo()
                {
                    TxHash = u.TxHash,
                    TxIndex = (uint)u.TxIndex,
                    OutputAddress = u.Address,
                    Balance = new()
                    {
                        Lovelaces = resolvedOutput.Value.Coin,
                        Assets = ConvertNativeAssetToBalanceAsset(resolvedOutput.Value.MultiAsset)
                    }
                };
            })
            .ToList();

        return resolvedUtxos;
    }

    public static string AbbreviateAmount(BigInteger amount, int decimals)
    {
        ulong ten = 10;
        ulong thousand = 1000;
        ulong million = 1000000;
        ulong billion = 1000000000;
        ulong trillion = 1000000000000;
        ulong quadrillion = 1000000000000000;

        BigInteger amountDec = BigInteger.Divide(amount, BigInteger.Pow(ten, decimals));

        switch (amountDec)
        {
            case var _ when amountDec >= quadrillion:
                BigInteger quadrillions = amountDec / quadrillion;
                return quadrillions > 999 ? "***Q" : $"{quadrillions}Q";
            case var _ when amountDec >= trillion:
                BigInteger trillions = amountDec / trillion;
                return $"{trillions}T";
            case var _ when amountDec >= billion:
                BigInteger billions = amountDec / billion;
                return $"{billions}B";
            case var _ when amountDec >= million:
                BigInteger millions = amountDec / million;
                return $"{millions}M";
            case var _ when amountDec >= thousand:
                BigInteger thousands = amountDec / thousand;
                return $"{thousands}K";
            default:
                return amountDec.ToString();
        }
    }

    public static string TimeToDateString(long time)
    {
        long s = time / 1000;
        long z = s / 86400 + 719468;
        long era = (z >= 0 ? z : z - 146096) / 146097;
        long doe = z - era * 146097;
        long yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
        long y = yoe + era * 400;
        long doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
        long mp = (5 * doy + 2) / 153;
        long d = doy - (153 * mp + 2) / 5 + 1;
        long m = mp + (mp < 10 ? 3 : -9);
        long adjustedY = y + (m <= 2 ? 1 : 0);

        string yearString = adjustedY.ToString().Substring(Math.Max(0, adjustedY.ToString().Length - 2)); // Get last two digits of the year
        string monthString = (m < 10 ? "0" : "") + m.ToString();
        string dayString = (d < 10 ? "0" : "") + d.ToString();

        return yearString + monthString + dayString;
    }

    public static SlotNetworkConfig SlotUtilityFromNetwork(NetworkType networkType) => networkType switch
    {
        NetworkType.Mainnet => SlotUtility.Mainnet,
        NetworkType.Preview => SlotUtility.Preview,
        NetworkType.Preprod => SlotUtility.Preprod,
        _ => throw new NotImplementedException()
    };

    public static long TimeFromSlot(NetworkType network, long slot) => SlotUtility.GetPosixTimeSecondsFromSlot(CoinectaUtils.SlotUtilityFromNetwork(network), slot);
}
