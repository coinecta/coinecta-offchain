using Cardano.Sync.Data.Models;
using Chrysalis.Cbor;

namespace Coinecta.Data.Models.Entity;

public record VestingTreasuryBySlot
{
    public uint Slot { get; init; } = 0;
    public string Id { get; init; } = default!;
    public string BlockHash { get; init; } = default!;
    public string TxHash { get; init; } = default!;
    public uint TxIndex { get; init; } = 0;
    public byte[] Datum { get; init; } = default!;
    public Value? Amount { get; init; } = default!;
    public UtxoStatus UtxoStatus { get; init; } = default!;
    public TreasuryDatum? TreasuryDatum => CborSerializer.Deserialize<TreasuryDatum>(Datum);
}