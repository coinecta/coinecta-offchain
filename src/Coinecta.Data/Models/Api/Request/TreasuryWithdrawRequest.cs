namespace Coinecta.Data.Models.Api.Request;

public record TreasuryWithdrawRequest(
    OutputReference? SpendOutRef,
    string? Id,
    string OwnerAddress,
    string RawCollateralUtxo
);