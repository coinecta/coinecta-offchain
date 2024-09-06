using Chrysalis.Cardano.Models.Coinecta.Vesting;
using Chrysalis.Cbor;

namespace Coinecta.Data.Models.Entity;

public record VestingClaimEntryByRootHash
{
    public string Id { get; init; } = default!;
    public string RootHash { get; init; } = default!;
    public string ClaimantPkh { get; init; } = default!;
    public byte[] ClaimEntryRaw { get; init; } = default!;

    public ClaimEntry? ClaimEntry => CborSerializer.Deserialize<ClaimEntry>(ClaimEntryRaw);
}