
namespace Coinecta.Data.Models.Reducers;

public enum UtxoStatus
{
    Unspent,
    Spent
}

public class UtxoByAddress
{
    public string Address { get; init; } = default!;
    public string TxHash { get; init; } = default!;
    public ulong TxIndex { get; init; }
    public ulong Slot { get; init; }
    public byte[]? TxOutCbor { get; init; } = default!;
    public UtxoStatus Status { get; set; } = UtxoStatus.Unspent;
}