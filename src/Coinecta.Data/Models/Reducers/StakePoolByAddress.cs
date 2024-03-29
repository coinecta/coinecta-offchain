using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Cardano.Sync.Data.Models;
using Coinecta.Data.Models.Datums;
using Coinecta.Data.Models.Enums;

namespace Coinecta.Data.Models.Reducers;
public record StakePoolByAddress
{
    public string Address { get; init; } = default!;
    public ulong Slot { get; init; }
    public string TxHash { get; init; } = default!;
    public ulong TxIndex { get; init; }
    public UtxoStatus UtxoStatus { get; set; } = UtxoStatus.Unspent;
    public Value Amount { get; init; } = default!;

    [NotMapped]
    public StakePool StakePool { get; set; } = default!;

    public JsonElement StakePoolJson
    {
        get
        {
            var jsonString = JsonSerializer.Serialize(StakePool);
            return JsonDocument.Parse(jsonString).RootElement;
        }

        set
        {
            if (value.ValueKind == JsonValueKind.Undefined || value.ValueKind == JsonValueKind.Null)
            {
                throw new Exception("Invalid StakePoolJson");
            }
            else
            {
                StakePool = JsonSerializer.Deserialize<StakePool>(value.GetRawText()) ?? throw new Exception("Invalid StakePoolJson");
            }
        }
    }
}
