using Cardano.Sync.Data.Models;

namespace Coinecta.Data.Models.Api.Request;

public record CreateTreasuryRequest(
    string OwnerAddress,
    string TreasuryRootHash,
    ulong UnlockTime,
    Value Amount,
    IEnumerable<string> RawUtxos
);