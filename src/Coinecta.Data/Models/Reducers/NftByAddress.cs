using Coinecta.Data.Models.Enums;

namespace Coinecta.Data.Models.Reducers;

public record NftByAddress
{
    public string Address { get; init; } = default!;
    public string TxHash { get; init; } = default!;
    public ulong OutputIndex { get; init; }
    public ulong Slot { get; init; }
    public string PolicyId { get; init; } = default!;
    public string AssetName { get; init; } = default!;
    public UtxoStatus UtxoStatus { get; set; }
}