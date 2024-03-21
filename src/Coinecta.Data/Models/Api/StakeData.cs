namespace Coinecta.Data.Models.Api;

public record StakeData
{
    public ulong Total { get; init; }
    public ulong Locked { get; init; }
    public ulong Unclaimed { get; init; }
}