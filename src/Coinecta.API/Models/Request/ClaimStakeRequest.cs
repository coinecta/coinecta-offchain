namespace Coinecta.API.Models.Request;

public record ClaimStakeRequest
{
    public IEnumerable<OutputReference> StakeUtxoOutputReferenceList { get; init; } = default!;
    public IEnumerable<string> WalletUtxoListCbor { get; init; } = default!;
}