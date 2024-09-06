namespace Coinecta.Data.Models.Entity;

public interface IVestingTreasury
{
    string Id { get; init; }
    ulong Slot { get; init; }
    string TxHash { get; init; }
    uint TxIndex { get; init; }
    string OwnerPkh { get; init; }
    byte[] UtxoRaw { get; init; }
}