using System.Formats.Cbor;
using CborSerialization;

namespace Coinecta.Data.Models.Datums;

// 121_0([_ 5, 100_0])
[CborSerialize(typeof(RationalCborConvert))]
public record Rational(ulong Numerator, ulong Denominator);

public class RationalCborConvert : ICborConvertor<Rational>
{
    public Rational Read(ref CborReader reader)
    {
        var tag = reader.ReadTag();
        if ((int)tag != 121)
        {
            throw new Exception("Invalid tag");
        }
        reader.ReadStartArray();
        var numerator = reader.ReadUInt64();
        var denominator = reader.ReadUInt64();
        reader.ReadEndArray();
        return new Rational(numerator, denominator);
    }

    public void Write(ref CborWriter writer, Rational value)
    {
        writer.WriteTag((CborTag)121);
        writer.WriteStartArray(null);
        writer.WriteUInt64(value.Numerator);
        writer.WriteUInt64(value.Denominator);
        writer.WriteEndArray();
    }
}