using System.Security.Cryptography.X509Certificates;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Models.Keys;
using Coinecta.Data.Models.Reducers;
using PublicKey = CardanoSharp.Wallet.Models.Keys.PublicKey;

namespace Coinecta.Catcher.Models;

public class CatcherState
{
    public List<StakePoolByAddress>? CurrentStakePoolStates { get; set; }
    public Utxo? CurrentCertificateUtxoState { get; set; }
    public Utxo? CurrentCollateralUtxoState { get; set; }
    public IEnumerable<PendingStakeRequest> PendingExecutionStakeRequests { get; set; } = [];
    public PublicKey CatcherPublicKey { get; set; } = default!;
    public PrivateKey CatcherPrivateKey { get; set; } = default!;
}