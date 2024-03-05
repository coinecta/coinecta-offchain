namespace Coinecta.Data.Models;

public record OutputReference
{
    public string TxHash { get; init; } = default!;
    public ulong Index { get; init; }
}