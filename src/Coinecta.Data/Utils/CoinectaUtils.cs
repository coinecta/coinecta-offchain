using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Models.Transactions;
using Coinecta.Data.Models;
using Microsoft.Extensions.Configuration;

namespace Coinecta.Data.Utils;

public static class CoinectaUtils
{
    public static TransactionInput GetTreasuryReferenceInput(IConfiguration configuration)
    {
        string txOutputCbor = configuration["TreasuryValidatorRefScriptTxOutCbor"] ?? throw new Exception("Treasury validator reference script tx output cbor not configured");
        string txHash = configuration["TreasuryValidatorRefScriptTxHash"] ?? throw new Exception("Treasury validator reference script tx hash not configured");
        int txIndex = configuration.GetValue("TreasuryValidatorRefScriptTxIndex", 0);
        TransactionOutput txOutput = TransactionUtils.DeserializeTxOutput(txOutputCbor);

        return new()
        {
            TransactionId = Convert.FromHexString(txHash),
            TransactionIndex = (uint)txIndex,
            Output = txOutput
        }; ;
    }
}