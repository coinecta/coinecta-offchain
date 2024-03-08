namespace Coinecta.Data.Models.Api.Request;

public record AddStakeRequest
{
    public StakePool StakePool { get; init; } = default!;
    public string OwnerAddress { get; init; } = default!;
    public string DestinationAddress { get; init; } = default!;
    public int RewardSettingIndex { get; init; }
    public IEnumerable<string> WalletUtxoListCbor { get; init; } = default!;
    public ulong Amount { get; init; }
}