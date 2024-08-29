using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Models.Addresses;
using CardanoSharp.Wallet.Models.Transactions.TransactionWitness.PlutusScripts;
using CardanoSharp.Wallet.TransactionBuilding;
using CardanoSharp.Wallet.Utilities;

namespace Coinecta.Data.Utils;

public static class AddressUtils
{
    public static Address GetScriptAddress(byte[] validatorScriptCbor, NetworkType networkType)
    {
        PlutusV2Script plutusScript = PlutusV2ScriptBuilder.Create
        .SetScript(validatorScriptCbor)
        .Build();

        return AddressUtility.GetEnterpriseScriptAddress(plutusScript, networkType);
    }
}