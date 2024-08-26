using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Cardano.Sync.Data.Models;
using Cardano.Sync.Data.Models.Datums;
using Coinecta.Data.Models.Datums;
using Value = Cardano.Sync.Data.Models.Value;

namespace Coinecta.Data.Models.Reducers;

public enum StakeRequestStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Error
}

public class StakeRequestByAddress
{
    public string Address { get; init; } = default!;
    public ulong Slot { get; init; }
    public string TxHash { get; init; } = default!;
    public ulong TxIndex { get; init; }
    public Value Amount { get; init; } = default!;
    public StakeRequestStatus Status { get; set; }

    [NotMapped]
    public StakePoolProxy<NoDatum> StakePoolProxy { get; set; } = default!;

    public JsonElement StakePoolJson
    {
        get
        {
            var jsonString = JsonSerializer.Serialize(StakePoolProxy);
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
                StakePoolProxy = JsonSerializer.Deserialize<StakePoolProxy<NoDatum>>(value.GetRawText()) ?? throw new Exception("Invalid StakePoolJson");
            }
        }
    }
}