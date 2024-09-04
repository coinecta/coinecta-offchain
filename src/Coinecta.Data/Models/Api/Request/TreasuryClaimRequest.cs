using Cardano.Sync.Data.Models;

namespace Coinecta.Data.Models;

public record TreasuryClaimRequest(
    OutputReference SpendOutRef,
    Value? DirectClaimValue,
    Value? VestedClaimValue,
    string OwnerAddress,
    string Redeemer,
    string ReturnDatum,
    string RawCollateralUtxo,
    IEnumerable<string> RawUtxos
);