using System.Text.Json;
using System.Text.Json.Serialization;

namespace Coinecta.Models.Api;

public class UlongToStringConverter : JsonConverter<ulong>
{
    public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string ulongString = reader.GetString() ?? string.Empty;
        return ulong.TryParse(ulongString, out ulong value) ? value : 0;
    }

    public override void Write(Utf8JsonWriter writer, ulong ulongValue, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ulongValue.ToString());
    }
}
