namespace Coinecta.Data.Models.Api;

public record PoolStats
{
    public string AssetName { get; set; } = default!;
    public Dictionary<decimal, int>? NftsByInterest { get; set; } = [];
    public Dictionary<decimal, ulong>? RewardsByInterest { get; set; } = [];
    public Dictionary<decimal, StakeData>? StakeDataByInterest { get; set; } = [];
    public Dictionary<string, int>? NftsByExpiration { get; set; } = [];
    public Dictionary<string, ulong>? RewardsByExpiration { get; set; } = [];
    public Dictionary<string, StakeData>? StakeDataByExpiration { get; set; } = [];
}