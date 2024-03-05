using CardanoSharp.Wallet.Models;
using Coinecta.Data.Models.Reducers;

namespace Coinecta.Data.Models.Api.Request;

public record ExecuteStakeRequest
{
    public StakePool? StakePool { get; init; } = default!;
    public OutputReference? StakeRequestOutputReference { get; init; } = default!;
    public StakePoolByAddress? StakePoolData { get; init; }
    public StakeRequestByAddress? StakeRequestData { get; init; }
    public IEnumerable<string>? WalletUtxoListCbor { get; init; } = default!;
    public Utxo? CollateralUtxo { get; init; }
    public Utxo? CertificateUtxo { get; init; }
}