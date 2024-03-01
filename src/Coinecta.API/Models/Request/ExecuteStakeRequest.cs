namespace Coinecta.API.Models.Request;

public record ExecuteStakeRequest
{
    public StakePool StakePool { get; init; } = default!;
    public OutputReference StakeRequestOutputReference { get; init; } = default!;
    public IEnumerable<string> WalletUtxoListCbor { get; init; } = default!;
}