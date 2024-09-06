using Cardano.Sync.Data.Models;

namespace Coinecta.Data.Models;

public record TreasuryClaimRequest(
    OutputReference? SpendOutRef,
    string? Id,
    string OwnerAddress,
    string RawCollateralUtxo,
    IEnumerable<string> RawUtxos
);