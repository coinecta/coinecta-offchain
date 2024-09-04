namespace Coinecta.Data.Models;

public record TreasuryWithdrawRequest(
    OutputReference SpendOutRef,
    string OwnerAddress,
    string RawCollateralUtxo
);