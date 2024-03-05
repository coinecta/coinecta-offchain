namespace Coinecta.Data.Models.Api.Request;

public record ClaimStakeRequest
{
    public IEnumerable<OutputReference> StakeUtxoOutputReferences { get; init; } = default!;
    public IEnumerable<string> WalletUtxoListCbor { get; init; } = default!;
}