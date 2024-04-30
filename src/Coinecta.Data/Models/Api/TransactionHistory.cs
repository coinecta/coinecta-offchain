using Cardano.Sync.Data.Models;

namespace Coinecta.Data.Models.Api;

public enum TransactionType
{
    StakeRequestPending,
    StakeRequestExecuted,
    StakeRequestCanceled,
    StakePositionCreated,
    StakePositionClaimed,
}
public record TransactionHistory
{
    public string Address { get; set; } = default!;
    public TransactionType Type { get; set; }
    public Asset Amount { get; set; } = default!;
    public ulong Slot { get; set; }
    public string TxHash { get; set; } = default!;
}