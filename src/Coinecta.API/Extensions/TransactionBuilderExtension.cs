
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.TransactionBuilding;
using CardanoSharp.Wallet.Utilities;

namespace Coinecta.API.Extensions;

public static class TransactionBuilderExtension
{

    public static Transaction BuildAndSetExUnits(this ITransactionBuilder builder, NetworkType networkType)
    {
        Transaction tx = builder.Build();
        CsBindgen.TransactionEvaluation txEvalResults = CsBindgen.UPLCMethods.GetExUnits(tx, networkType);

        // add ex units to transaction
        tx.TransactionWitnessSet.Redeemers = txEvalResults.Redeemers!;
        tx.TransactionBody.ScriptDataHash = ScriptUtility
            .GenerateScriptDataHash(txEvalResults.Redeemers!, [], CostModelUtility.PlutusV2CostModel.Serialize());

        return tx;
    }
}