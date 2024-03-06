using Coinecta.Data.Models.Api;

namespace Coinecta.Data.Models.Response;

public record StakeSummaryResponse
{
    public Dictionary<string, StakeStats> PoolStats { get; set; } = [];
    public StakeStats TotalStats { get; set; } = new();
}