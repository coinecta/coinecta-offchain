namespace Coinecta.Data.Models.Api.Request;

public record CancelStakeRequest
{
    public OutputReference StakeRequestOutputReference { get; init; } = default!;
    public IEnumerable<string> WalletUtxoListCbor { get; init; } = default!;
    public string? CollateralUtxoCbor { get; init; } = default;
}