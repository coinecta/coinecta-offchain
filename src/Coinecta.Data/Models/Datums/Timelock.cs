using System.Formats.Cbor;
using CborSerialization;

namespace Coinecta.Data.Models.Datums;

/*
121_0([_
    1000_1,
    h'6c00ac8ecdbfad86c9287b2aec257f2e3875b572de8d8df27fd94dd650671c94',
])
*/
[CborSerialize(typeof(TimelockCborConvert))]
public record Timelock(ulong Lockuntil, byte[] TimeLockKey) : IDatum;

public class TimelockCborConvert : ICborConvertor<Timelock>
{
    public Timelock Read(ref CborReader reader)
    {
        var tag = reader.ReadTag();
        if ((int)tag != 121)
        {
            throw new Exception("Invalid tag");
        }

        reader.ReadStartArray();

        var lockUntil = reader.ReadUInt64();
        var timeLockKey = reader.ReadByteString();

        reader.ReadEndArray();

        return new Timelock(lockUntil, timeLockKey);
    }

    public void Write(ref CborWriter writer, Timelock value)
    {
        writer.WriteTag((CborTag)121);
        writer.WriteStartArray(null);
        writer.WriteUInt64(value.Lockuntil);
        writer.WriteByteString(value.TimeLockKey);
        writer.WriteEndArray();
    }
}