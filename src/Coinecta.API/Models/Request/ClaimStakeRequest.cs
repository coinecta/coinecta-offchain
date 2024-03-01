namespace Coinecta.API.Models.Request;

public record ClaimStakeRequest
{
    public IEnumerable<OutputReference> StakeUtxoOutputReferences { get; init; } = default!;
    public IEnumerable<string> WalletUtxoListCbor { get; init; } = default!;
}