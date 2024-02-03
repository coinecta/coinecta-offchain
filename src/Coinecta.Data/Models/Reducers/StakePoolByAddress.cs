using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Coinecta.Data.Models.Datums;

namespace Coinecta.Data.Models.Reducers;
public record StakePoolByAddress
{
    public string Address { get; init; } = default!;

    [NotMapped]
    public Signature Signature { get; set; } = default!;

    [NotMapped]
    public List<RewardSetting> RewardSettings { get; set; } = default!;

    public JsonElement SignatureJson
    {
        get
        {
            var jsonString = JsonSerializer.Serialize(Signature);
            return JsonDocument.Parse(jsonString).RootElement;
        }

        set
        {
            if (value.ValueKind == JsonValueKind.Undefined || value.ValueKind == JsonValueKind.Null)
            {
                Signature = new Signature([]);
            }
            else
            {
                Signature = JsonSerializer.Deserialize<Signature>(value.GetRawText()) ?? new Signature([]);
            }
        }
    }

    public JsonElement RewardSettingsJson
    {
        get
        {
            var jsonString = JsonSerializer.Serialize(RewardSettings);
            return JsonDocument.Parse(jsonString).RootElement;
        }

        set
        {
            if (value.ValueKind == JsonValueKind.Undefined || value.ValueKind == JsonValueKind.Null)
            {
                RewardSettings = [];
            }
            else
            {
                RewardSettings = JsonSerializer.Deserialize<List<RewardSetting>>(value.GetRawText()) ?? [];
            }
        }
    }

    public string PolicyId { get; init; } = default!;
    public string AssetName { get; init; } = default!;
    public ulong Decimals { get; init; }
    public string TxHash { get; init; } = default!;
    public ulong TxIndex { get; init; }
    public Value Amount { get; init; } = default!;
    public ulong Slot { get; init; }
}
