namespace Coinecta.API.Models;

public record OutputReference
{
    public string TxHash { get; init; } = default!;
    public ulong Index { get; init; }
}