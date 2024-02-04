using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Coinecta.Data.Models.Datums;

namespace Coinecta.Data.Models.Reducers;

public class StakeRequestByAddress
{
    public string Address { get; init; } = default!;
    public ulong Slot { get; init; }
    public string TxHash { get; init; } = default!;
    public ulong TxIndex { get; init; }
}