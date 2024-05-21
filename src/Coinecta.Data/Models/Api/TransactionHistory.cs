using System.ComponentModel.DataAnnotations.Schema;
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
    public string? TxType { get; set; }
    public ulong Lovelace { get; set; }
    public Dictionary<string, Dictionary<string, ulong>>? Assets { get; set; }
    public string TxHash { get; set; } = string.Empty;
    public ulong? TxIndex { get; set; }
    public long CreatedAt { get; set; }

    // Stake Request
    public string? LockDuration { get; set; }

    // Stake Position
    public ulong? UnlockTime { get; set; }
    public string? StakeKey { get; set; } = string.Empty;
    public string? TransferredToAddress { get; set; }
}