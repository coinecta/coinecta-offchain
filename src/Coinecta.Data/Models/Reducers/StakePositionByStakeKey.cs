using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Coinecta.Data.Models.Datums;

namespace Coinecta.Data.Models.Reducers;

public record StakePositionByStakeKey
{
    public string StakeKey { get; init; } = default!;
    public ulong Slot { get; init; }
    public string TxHash { get; init; } = default!;
    public ulong TxIndex { get; init; }
    public Value Amount { get; init; } = default!;
    
    [NotMapped]
    public CIP68<Timelock> StakePosition { get; set; } = default!;
    
    public JsonElement StakePositionJson
    {
        get
        {
            var jsonString = JsonSerializer.Serialize(StakePosition);
            return JsonDocument.Parse(jsonString).RootElement;
        }

        set
        {
            if (value.ValueKind == JsonValueKind.Undefined || value.ValueKind == JsonValueKind.Null)
            {
                throw new Exception("Invalid StakePositionJson");
            }
            else
            {
                StakePosition = JsonSerializer.Deserialize<CIP68<Timelock>>(value.GetRawText()) ?? throw new Exception("Invalid StakePositionJson");
            }
        }
    }
}