namespace Coinecta.Data.Models;

public record Value
{
    public ulong Coin { get; init; }
    public Dictionary<string, Dictionary<string, ulong>> MultiAsset { get; set; }
}