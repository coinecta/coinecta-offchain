using Cardano.Sync.Data.Models;

namespace Coinecta.Data.Models;

public record TreasuryWithdrawRequest(
    OutputReference SpendOutRef,
    Value LockedValue,
    string OwnerAddress,
    string Redeemer,
    string Datum,
    string RawCollateralUtxo
);