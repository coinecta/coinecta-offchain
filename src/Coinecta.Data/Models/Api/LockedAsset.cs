namespace Coinecta.Data.Models.Api;

public record LockedAsset
{
    public string PolicyId { get; init; } = default!;
    public string AssetName { get; init; } = default!;
    public ulong Amount { get; init; }
}