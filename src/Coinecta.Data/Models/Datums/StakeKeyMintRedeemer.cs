using System.Formats.Cbor;
using CborSerialization;

namespace Coinecta.Data.Models.Datums;

/*
121_0([_
    121_0([_
        h'cb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3',
    ]),
])
*/
[CborSerialize(typeof(StakeKeyMintRedeemerCborConvert))]
public record StakeKeyMintRedeemer(ulong StakePoolindex, ulong TimeLockIndex) : IDatum;

public class StakeKeyMintRedeemerCborConvert : ICborConvertor<StakeKeyMintRedeemer>
{
    public StakeKeyMintRedeemer Read(ref CborReader reader)
    {
        var tag = reader.ReadTag();
        if ((int)tag != 121)
        {
            throw new Exception("Invalid tag");
        }
        reader.ReadStartArray();
        var stakePoolIndex = reader.ReadUInt64();
        var timeLockIndex = reader.ReadUInt64();
        reader.ReadEndArray();
        return new StakeKeyMintRedeemer(stakePoolIndex, timeLockIndex);
    }

    public void Write(ref CborWriter writer, StakeKeyMintRedeemer value)
    {
        writer.WriteTag((CborTag)121);
        writer.WriteStartArray(null);
        writer.WriteUInt64(value.StakePoolindex);
        writer.WriteUInt64(value.TimeLockIndex);
        writer.WriteEndArray();
    }
}