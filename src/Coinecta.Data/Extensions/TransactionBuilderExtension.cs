using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions.TransactionWitness.PlutusScripts;
using CardanoSharp.Wallet.TransactionBuilding;
using CardanoSharp.Wallet.Utilities;

namespace Coinecta.Data.Extensions;

public static class TransactionBuilderExtension
{
    public static Transaction BuildAndSetExUnits(this ITransactionBuilder builder, NetworkType networkType, List<IPlutusData>? datums = null)
    {
        Transaction tx = builder.Build();

        CsBindgen.TransactionEvaluation txEvalResults = CsBindgen.UPLCMethods.GetExUnits(tx, networkType);

        if (txEvalResults.Error != null)
        {
            throw new Exception($"Error evaluating transaction: {txEvalResults.Error}");
        }

        datums ??= [];

        // add ex units to transaction
        tx.TransactionWitnessSet.Redeemers = txEvalResults.Redeemers!;
        tx.TransactionBody.ScriptDataHash = ScriptUtility
            .GenerateScriptDataHash(txEvalResults.Redeemers!, datums, CostModelUtility.PlutusV2CostModel.Serialize());

        return tx;
    }
}