namespace Coinecta.Data.Models.Api;

public record PoolStats
{
    public string AssetName { get; set; } = default!;
    public Dictionary<ulong, int>? NftsByInterest { get; set; } = [];
    public Dictionary<ulong, ulong>? RewardsByInterest { get; set; } = [];
    public Dictionary<ulong, StakeData>? StakeDataByInterest { get; set; } = [];
    public Dictionary<string, int>? NftsByExpiration { get; set; } = [];
    public Dictionary<string, ulong>? RewardsByExpiration { get; set; } = [];
    public Dictionary<string, StakeData>? StakeDataByExpiration { get; set; } = [];
}