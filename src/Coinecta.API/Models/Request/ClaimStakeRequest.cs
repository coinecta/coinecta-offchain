namespace Coinecta.API.Models.Request;

public record ClaimStakeRequest
{
    public OutputReference StakeUtxoOutputReference { get; init; } = default!;
    public IEnumerable<string> WalletUtxoListCbor { get; init; } = default!;
}