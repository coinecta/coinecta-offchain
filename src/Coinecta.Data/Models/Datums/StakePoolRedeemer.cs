using System.Formats.Cbor;
using Cardano.Sync.Data.Models.Datums;
using CborSerialization;

namespace Coinecta.Data.Models.Datums;

[CborSerialize(typeof(StakePoolRedeemerCborConvert))]
public record StakePoolRedeemer(ulong RewardIndex) : IDatum;

public class StakePoolRedeemerCborConvert : ICborConvertor<StakePoolRedeemer>
{
    public StakePoolRedeemer Read(ref CborReader reader)
    {
        var tag = reader.ReadTag();
        if ((int)tag != 122) // Replace 121 with the actual tag used for your data, if necessary
        {
            throw new Exception("Invalid tag");
        }
        reader.ReadStartArray();
        reader.ReadTag();
        if ((int)tag != 121)
        {
            throw new Exception("Invalid tag");
        }
        reader.ReadStartArray();
        ulong rewardIndex = reader.ReadUInt64();
        reader.ReadEndArray();
        reader.ReadEndArray();

        return new StakePoolRedeemer(rewardIndex);
    }

    public void Write(ref CborWriter writer, StakePoolRedeemer value)
    {
        writer.WriteTag((CborTag)122);
        writer.WriteStartArray(null);
        writer.WriteTag((CborTag)121);
        writer.WriteStartArray(null);
        writer.WriteUInt64(value.RewardIndex);
        writer.WriteEndArray();
        writer.WriteEndArray();
    }
}