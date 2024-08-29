using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions.TransactionWitness;
using CardanoSharp.Wallet.TransactionBuilding;
using Coinecta.Data.Utils;

namespace Coinecta.Data.Extensions;

public static class TransactionExtension
{
    public static string Sign(this Transaction self, string txWitnessCbor)
    {
        TransactionWitnessSet witnessSet = TransactionUtils.DeserializeTxWitnessSet(txWitnessCbor);
        ITransactionWitnessSetBuilder witnessSetBuilder = TransactionWitnessSetBuilder.Create;
        witnessSet.VKeyWitnesses.ToList().ForEach((witness) => witnessSetBuilder.AddVKeyWitness(witness));

        if (self.TransactionWitnessSet is null)
            self.TransactionWitnessSet = witnessSetBuilder.Build();
        else
            self.TransactionWitnessSet.VKeyWitnesses = witnessSet.VKeyWitnesses;

        byte[] serializedSignedTx = self.Serialize();
        int maxTransactionSize = 16 * 1024; // 16KB

        if (serializedSignedTx.Length > maxTransactionSize)
            throw new Exception("Transaction size exceeds 16KB");

        return Convert.ToHexString(serializedSignedTx);
    }
}