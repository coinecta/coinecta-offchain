using System.Text.Json;
using Cardano.Sync.Data.Models;

namespace Coinecta.Data.Models.Api;

public enum TransactionType
{
    StakeRequestPending,
    StakeRequestExecuted,
    StakeRequestCanceled,
    StakePositionReceived,
    StakePositionTransferred,
    StakePositionRedeemed
}

public record TransactionHistory
{
    public string Address { get; set; } = string.Empty;
    public TransactionType? Type { get; set; }
    public Asset Amount { get; set; } = new();
    public ulong Slot { get; set; }
    public string TxHash { get; set; } = string.Empty;

    // Stake Request
    public ulong? LockDuration { get; set; }

    // Stake Position
    public ulong? UnlockTime { get; set; }
    public string? StakeKey { get; set; } = string.Empty;
    public string? TransferredToAddress { get; set; }
    public ulong? OutputIndex { get; set; }
}
