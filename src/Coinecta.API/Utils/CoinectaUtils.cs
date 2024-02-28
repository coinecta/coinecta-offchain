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

namespace Coinecta.API.Utils;

public static class CoinectaUtils
{
    public static IEnumerable<Utxo> ConvertUtxoListCbor(IEnumerable<string> utxoCbors)
    {
        return utxoCbors.Select(utxoCbor =>
        {
            var utxoCborObj = CBORObject.DecodeFromBytes(Convert.FromHexString(utxoCbor));
            return utxoCborObj.GetUtxo();
        }).ToList();
    }

    public static IEnumerable<TransactionOutput> ConvertTxOutputListCbor(IEnumerable<string> txOutputCbors)
    {
        return txOutputCbors.Select(txOutputCbor =>
        {
            var txOutputCborObj = CBORObject.DecodeFromBytes(Convert.FromHexString(txOutputCbor));
            return txOutputCborObj.GetTransactionOutput();
        }).ToList();
    }

    public static TransactionWitnessSet ConvertTxWitnessSetCbor(string txWitnessSetCbor)
    {
        var txWitnessSetCborObj = CBORObject.DecodeFromBytes(Convert.FromHexString(txWitnessSetCbor));
        return txWitnessSetCborObj.GetTransactionWitnessSet();
    }

    public static Address ConvertAddressCbor(string addressCbor)
    {
        return new Address(Convert.FromHexString(addressCbor));
    }

    public static Address ValidatorAddress(byte[] validatorScriptCbor)
    {
        var plutusScript = PlutusV2ScriptBuilder.Create
        .SetScript(validatorScriptCbor)
        .Build();

        return ValidatorAddress(plutusScript);
    }

    public static Address ValidatorAddress(PlutusV2Script plutusScript)
    {
        return AddressUtility.GetEnterpriseScriptAddress(plutusScript, NetworkType.Preview);
    }
}