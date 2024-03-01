namespace Coinecta.API.Models;

public record StakePool
{
    public string Address { get; init; } = default!;
    public string OwnerPkh { get; init; } = default!;
    public string PolicyId { get; init; } = default!;
    public string AssetName { get; init; } = default!;
}