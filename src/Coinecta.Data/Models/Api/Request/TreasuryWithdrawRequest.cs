namespace Coinecta.Data.Models;

public record TreasuryWithdrawRequest(
    OutputReference? SpendOutRef,
    string? Id,
    string OwnerAddress,
    string RawCollateralUtxo
);