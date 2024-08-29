using CardanoSharp.Wallet.CIPs.CIP2;
using CardanoSharp.Wallet.CIPs.CIP2.ChangeCreationStrategies;
using CardanoSharp.Wallet.CIPs.CIP2.Models;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Extensions.Models.Transactions.TransactionWitnesses;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions.TransactionWitness;
using CardanoSharp.Wallet.TransactionBuilding;
using PeterO.Cbor2;

namespace Coinecta.Data.Utils;

public static class TransactionUtils
{
    public static CoinSelection GetCoinSelection(
        IEnumerable<TransactionOutput> outputs,
        IEnumerable<Utxo> utxos, string changeAddress,
        ITokenBundleBuilder? mint = null,
        List<Utxo>? requiredUtxos = null,
        List<ICertificateBuilder>? certificates = null,
        int limit = 20, ulong feeBuffer = 0uL
    )
    {
        RandomImproveStrategy coinSelectionStrategy = new();
        SingleTokenBundleStrategy changeCreationStrategy = new();
        CoinSelectionService coinSelectionService = new(coinSelectionStrategy, changeCreationStrategy);

        int retry = 0;

        while (retry < 100)
        {
            try
            {
                CoinSelection result = coinSelectionService
                    .GetCoinSelection(
                        outputs.ToList(),
                        utxos.ToList(),
                        changeAddress,
                        mint,
                        certificates,
                        requiredUtxos,
                        limit,
                        feeBuffer);

                return result;
            }
            catch
            {
                retry++;
            }
        }

        throw new Exception("Coin selection failed");
    }

    public static IEnumerable<Utxo> DeserializeUtxoCborHex(IEnumerable<string> utxoCbors)
    {
        return utxoCbors.Select(utxoCbor =>
        {
            CBORObject utxoCborObj = CBORObject.DecodeFromBytes(Convert.FromHexString(utxoCbor));
            return utxoCborObj.GetUtxo();
        }).ToList();
    }

    public static TransactionWitnessSet DeserializeTxWitnessSet(string txWitnessSetCbor)
    {
        CBORObject txWitnessSetCborObj = CBORObject.DecodeFromBytes(Convert.FromHexString(txWitnessSetCbor));
        return txWitnessSetCborObj.GetTransactionWitnessSet();
    }

    public static TransactionOutput DeserializeTxOutput(string txOutputCbor)
    {
        CBORObject txOutputCborObj = CBORObject.DecodeFromBytes(Convert.FromHexString(txOutputCbor));
        return txOutputCborObj.GetTransactionOutput();
    }
}