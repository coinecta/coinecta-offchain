namespace Coinecta.Data.Models.Api;

public record TransactionHistoryRaw
{
    public string Address { get; set; } = string.Empty;
    public string? TxType { get; set; }
    public ulong Lovelace { get; set; }
    public string? Assets { get; set; } = string.Empty;

    public ulong Slot { get; set; }
    public string TxHash { get; set; } = string.Empty;

    // Stake Request
    public string? LockDuration { get; set; }

    // Stake Position
    public ulong? UnlockTime { get; set; }
    public string? StakeKey { get; set; } = string.Empty;
    public string? TransferredToAddress { get; set; }
    public ulong? OutputIndex { get; set; }
    public int TotalCount { get; set; }
}