using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Models.Addresses;
using CardanoSharp.Wallet.Models.Transactions;

namespace Coinecta.Data.Extensions;

public static class TransactionInputExtension
{
    public static Utxo ToUtxo(this TransactionInput self) => new()
    {
        TxHash = Convert.ToHexString(self.TransactionId),
        TxIndex = self.TransactionIndex,
        Balance = self.Output?.Value.GetBalance() ?? new(),
        OutputAddress = new Address(self.Output?.Address!).ToStringHex(),
        OutputDatumOption = self.Output?.DatumOption,
        OutputScriptReference = self.Output?.ScriptReference
    };
}