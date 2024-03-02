using CardanoSharp.Wallet.Models;
using Coinecta.Data.Models.Reducers;

namespace Coinecta.API.Models.Request;

public record ExecuteStakeRequest
{
    public StakePool StakePool { get; init; } = default!;
    public OutputReference StakeRequestOutputReference { get; init; } = default!;
    public StakePoolByAddress? StakePoolData { get; init; }
    public IEnumerable<string> WalletUtxoListCbor { get; init; } = default!;
}