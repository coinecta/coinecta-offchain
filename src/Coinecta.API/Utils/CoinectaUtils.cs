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
using Coinecta.API.Models;
using CardanoSharp.Wallet.CIPs.CIP2.Models;
using CardanoSharp.Wallet.CIPs.CIP2;
using CardanoSharp.Wallet.CIPs.CIP2.ChangeCreationStrategies;

namespace Coinecta.API.Utils;

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

    public static Address ValidatorAddress(byte[] validatorScriptCbor)
    {
        PlutusV2Script plutusScript = PlutusV2ScriptBuilder.Create
        .SetScript(validatorScriptCbor)
        .Build();

        return ValidatorAddress(plutusScript);
    }

    public static Address ValidatorAddress(PlutusV2Script plutusScript)
    {
        return AddressUtility.GetEnterpriseScriptAddress(plutusScript, NetworkType.Preview);
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
        OutputReference txOutputRef = new OutputReference
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
        OutputReference txOutputRef = new OutputReference
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
        OutputReference txOutputRef = new OutputReference
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
        OutputReference txOutputRef = new OutputReference
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
}
