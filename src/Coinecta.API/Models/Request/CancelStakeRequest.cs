namespace Coinecta.API.Models.Request;

public record CancelStakeRequest
{
    public OutputReference StakeRequestOutputReference { get; init; } = default!;
    public IEnumerable<string> WalletUtxoListCbor { get; init; } = default!;
}