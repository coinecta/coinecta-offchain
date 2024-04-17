namespace Coinecta.Data.Models;

public record StakeTier
{
    public ulong Threshold { get; set; }
    public int Weight { get; set; }
}