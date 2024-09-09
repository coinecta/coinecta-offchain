using Cardano.Sync.Data.Models;

namespace Coinecta.Data.Models.Api.Request;

public record TreasuryClaimRequest(
    OutputReference? SpendOutRef,
    string? Id,
    string OwnerAddress,
    string UpdatedRootHash,
    string RawProof,
    string RawClaimEntry,
    string RawCollateralUtxo,
    IEnumerable<string> RawUtxos
);