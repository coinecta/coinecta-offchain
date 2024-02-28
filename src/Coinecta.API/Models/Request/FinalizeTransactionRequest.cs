namespace Coinecta.API.Models.Request;

public record FinalizeTransactionRequest
{
    public string UnsignedTxCbor { get; init; } = default!;
    public string TxWitnessCbor { get; init; } = default!;
}