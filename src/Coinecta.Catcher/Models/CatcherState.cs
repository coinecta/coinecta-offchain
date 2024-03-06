using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Models.Addresses;
using CardanoSharp.Wallet.Models.Keys;
using Coinecta.Data.Models.Reducers;
using PublicKey = CardanoSharp.Wallet.Models.Keys.PublicKey;

namespace Coinecta.Catcher.Models;

public class CatcherState
{
    public List<StakePoolByAddress>? CurrentStakePoolStates { get; set; }
    public Utxo? CurrentCertificateUtxoState { get; set; }
    public Utxo? CurrentCollateralUtxoState { get; set; }
    public List<PendingStakeRequest> PendingExecutionStakeRequests { get; set; } = [];
    public PublicKey CatcherPublicKey { get; set; } = default!;
    public PrivateKey CatcherPrivateKey { get; set; } = default!;
    public Address CatcherAddress { get; set; } = default!;
    public string SubmitApiUrl { get; set; } = default!;
    public string CatcherCertificatePolicyId { get; set; } = default!;
    public string CatcherCertificateAssetName { get; set; } = default!;
}