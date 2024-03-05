using CardanoSharp.Wallet.Models;
using Coinecta.Data.Models;
using Coinecta.Data.Models.Reducers;

namespace Coinecta.Catcher.Models;

public class PendingStakeRequest
{
    public OutputReference? StakeRequestOutRef { get; set; }
    public ulong TTL { get; set; }
}