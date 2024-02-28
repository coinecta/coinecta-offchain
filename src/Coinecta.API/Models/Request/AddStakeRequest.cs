namespace Coinecta.API.Models.Request;

public record AddStakeRequest
{
    public OutputReference PoolOutputReference { get; init; } = default!;
    public string OwnerAddress { get; init; } = default!;
    public string DestinationAddress { get; init; } = default!;
    public int RewardSettingIndex { get; init; }
    public IEnumerable<string> WalletUtxoListCbor { get; init; } = default!;
    public long Amount { get; init; }
}