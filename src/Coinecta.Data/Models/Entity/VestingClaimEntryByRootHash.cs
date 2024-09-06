using CClaimEntry = Chrysalis.Cardano.Models.Coinecta.Vesting.ClaimEntry;
using Chrysalis.Cbor;

namespace Coinecta.Data.Models.Entity;

public record VestingClaimEntryByRootHash
{
    public string Id { get; init; } = default!;
    public string RootHash { get; init; } = default!;
    public string ClaimantPkh { get; init; } = default!;
    public byte[] ClaimEntryRaw { get; init; } = default!;

    public CClaimEntry? ClaimEntry => CborSerializer.Deserialize<CClaimEntry>(ClaimEntryRaw);
}