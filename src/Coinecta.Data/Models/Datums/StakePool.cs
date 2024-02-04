using System.Formats.Cbor;
using CborSerialization;

namespace Coinecta.Data.Models.Datums;

/*
121_0([_
    [_ 121_0([_ 300000_2, 121_0([_ 5, 100_0])])], Array<RewardSetting>
    h'8b05e87a51c1d4a0fa888d2bb14dbc25e8c343ea379a171b63aa84a0', // PolicyId: bytes
    h'434e4354', // AssetName: bytes
    121_0([_
        h'0c61f135f652bc17994a5411d0a256de478ea24dbc19759d2ba14f03',
    ]), // Signature
    0, // Decimals: uint64
])
*/
[CborSerialize(typeof(StakePoolCborConvert))]
public record StakePool(Signature Owner, RewardSetting[] RewardSettings, byte[] PolicyId, byte[] AssetName, ulong Decimals) : IDatum;

public class StakePoolCborConvert : ICborConvertor<StakePool>
{
    public StakePool Read(ref CborReader reader)
    {
        var tag = reader.ReadTag();
        if ((int)tag != 121)
        {
            throw new Exception("Invalid tag");
        }

        reader.ReadStartArray();

        // Read RewardSettings array
        reader.ReadStartArray();
        var rewardSettingsList = new List<RewardSetting>();
        while (reader.PeekState() != CborReaderState.EndArray)
        {
            var rewardSetting = new RewardSettingCborConvert().Read(ref reader);
            rewardSettingsList.Add(rewardSetting);
        }
        reader.ReadEndArray();
        var rewardSettings = rewardSettingsList.ToArray();

        // Read PolicyId
        var policyId = reader.ReadByteString();

        // Read AssetName
        var assetName = reader.ReadByteString();

        // Read Signature
        var owner = new SignatureCborConvert().Read(ref reader);

        // Read Decimals
        var decimals = reader.ReadUInt64();

        reader.ReadEndArray(); // End of StakePool

        return new StakePool(owner, rewardSettings, policyId, assetName, decimals);
    }

    public void Write(ref CborWriter writer, StakePool value)
    {
        writer.WriteTag((CborTag)121);
        writer.WriteStartArray(null); 

        // Write RewardSettings array
        writer.WriteStartArray(null);
        foreach (var rewardSetting in value.RewardSettings)
        {
            new RewardSettingCborConvert().Write(ref writer, rewardSetting);
        }
        writer.WriteEndArray();

        // Write PolicyId
        writer.WriteByteString(value.PolicyId);

        // Write AssetName
        writer.WriteByteString(value.AssetName);

        // Write Signature
        new SignatureCborConvert().Write(ref writer, value.Owner);

        // Write Decimals
        writer.WriteUInt64(value.Decimals);

        writer.WriteEndArray(); // End of StakePool
    }
}
