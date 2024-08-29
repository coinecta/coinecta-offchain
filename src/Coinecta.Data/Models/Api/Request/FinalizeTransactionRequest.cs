namespace Coinecta.Data.Models;

public record FinalizeTransactionRequest(string UnsignedTxCbor, string TxWitnessCbor);