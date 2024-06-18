
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Coinecta.Data.Models.Enums;

namespace Coinecta.Data.Models.Reducers;

public class UtxoByAddress
{
    public string Address { get; init; } = default!;
    public DateTimeOffset LastUpdated { get; set; }
    public DateTimeOffset LastRequested { get; set; }

    [NotMapped]
    public List<string> UtxoListCbor { get; set; } = default!;

    public byte[] UtxoListCborBytes
    {
        get => JsonSerializer.SerializeToUtf8Bytes(UtxoListCbor);
        set => UtxoListCbor = JsonSerializer.Deserialize<List<string>>(value) ?? [];
    }
}