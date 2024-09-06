namespace Coinecta.Data.Models.Api.Request;

public record FinalizeTransactionRequest(string UnsignedTxCbor, string TxWitnessCbor);