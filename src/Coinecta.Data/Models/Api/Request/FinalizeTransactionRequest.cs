namespace Coinecta.Data.Models.Api.Request;

public record FinalizeTransactionRequest
{
    public string UnsignedTxCbor { get; init; } = default!;
    public string TxWitnessCbor { get; init; } = default!;
}