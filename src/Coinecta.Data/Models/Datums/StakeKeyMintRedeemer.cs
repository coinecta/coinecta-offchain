using System.Formats.Cbor;
using Cardano.Sync.Data.Models.Datums;
using CborSerialization;

namespace Coinecta.Data.Models.Datums;

/* 121_0([_ 0, 1, 122_0([])]) */
[CborSerialize(typeof(StakeKeyMintRedeemerCborConvert))]
public record StakeKeyMintRedeemer(ulong StakePoolindex, ulong TimeLockIndex, bool Mint) : IDatum;

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
        var mintTag = reader.ReadTag();
        if ((int)mintTag != 121 && (int)mintTag != 122)
        {
            throw new Exception("Invalid tag");
        }
        var mint = mintTag == (CborTag)122;
        reader.ReadStartArray();
        reader.ReadEndArray();
        reader.ReadEndArray();
        return new StakeKeyMintRedeemer(stakePoolIndex, timeLockIndex, mint);
    }

    public void Write(ref CborWriter writer, StakeKeyMintRedeemer value)
    {
        writer.WriteTag((CborTag)121);
        writer.WriteStartArray(null);
        writer.WriteUInt64(value.StakePoolindex);
        writer.WriteUInt64(value.TimeLockIndex);

        if (value.Mint)
        {
            writer.WriteTag((CborTag)122);
        }
        else
        {
            writer.WriteTag((CborTag)121);
        }
        writer.WriteStartArray(0);
        writer.WriteEndArray();
        writer.WriteEndArray();
    }
}