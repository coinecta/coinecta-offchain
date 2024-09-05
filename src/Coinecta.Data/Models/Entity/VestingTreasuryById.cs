using Cardano.Sync.Data.Models;
using Chrysalis.Cardano.Models.Coinecta.Vesting;
using Chrysalis.Cbor;
namespace Coinecta.Data.Models.Entity;

public record VestingTreasuryById
{
    public string Id { get; init; } = default!;
    public uint Slot { get; init; } = 0;
    public string TxHash { get; init; } = default!;
    public uint TxIndex { get; init; } = 0;
    public string OwnerPkh { get; init; } = default!;
    public byte[] Datum { get; init; } = default!;
    public Value? Amount { get; init; } = default!;

    public Treasury? TreasuryDatum => CborSerializer.Deserialize<Treasury>(Datum);
}