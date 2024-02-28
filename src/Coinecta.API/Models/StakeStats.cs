namespace Coinecta.API.Models;

public record StakeStats
{
    public ulong TotalPortfolio { get; set; }
    public ulong TotalStaked { get; set; }
    public ulong TotalVested { get; set; }
    public ulong UnclaimedTokens { get; set; }
}