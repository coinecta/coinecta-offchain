namespace Coinecta.API.Models.Response;

public record StakeSummaryResponse
{
    public Dictionary<string, StakeStats> PoolStats { get; set; } = [];
    public StakeStats TotalStats { get; set; } = new();
}