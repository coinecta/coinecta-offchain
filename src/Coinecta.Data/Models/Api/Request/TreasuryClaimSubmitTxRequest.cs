namespace Coinecta.Data.Models.Api.Request;

public record TreasuryClaimSubmitTxRequest(
    string Id,
    string OwnerPkh,
    string UtxoRaw,
    string TxRaw
);