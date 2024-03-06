using CardanoSharp.Wallet.Models;
using Coinecta.Data.Models;
using Coinecta.Data.Models.Reducers;

namespace Coinecta.Catcher.Models;

public class PendingStakeRequest
{
    public string OutRef { get; set; } = default!;
    public ulong TTL { get; set; }
}