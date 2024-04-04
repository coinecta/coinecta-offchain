using System.Formats.Cbor;
using Cardano.Sync.Data.Models;
using Cardano.Sync.Data.Models.Datums;
using CborSerialization;

namespace Coinecta.Data.Models.Datums;

/*
StakePoolProxy with Destination No Datum
121_0([_
    121_0([_
        h'cb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3',
    ]), -> Signature
    121_0([_
        121_0([_
            121_0([_
                h'cb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3',
            ]),
            121_0([_
                121_0([_
                    121_0([_
                        h'cb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3',
                    ]),
                ]),
            ]),
        ]),
        121_0([]),
    ]), -> Destination
    1000_1, -> LockTime
    121_0([_ 1, 100_0]), -> RewardMultiplier
    h'8b05e87a51c1d4a0fa888d2bb14dbc25e8c343ea379a171b63aa84a0', -> PolicyId
    h'434e4354', -> AssetName
    h'5496b3318f8ca933bbfdf19b8faa7f948d044208e0278d62c24ee73e', -> StakeMintingPolicyId
])
*/
[CborSerialize(typeof(StakePoolProxyCborConvert<>))]
public record StakePoolProxy<T>(
    Signature Owner,
    Destination<T> Destination,
    ulong LockTime,
    Rational RewardMultiplier,
    byte[] PolicyId,
    byte[] AssetName,
    ulong AssetAmount,
    ulong LovelaceAmount,
    byte[] StakeMintingPolicyId
) : IDatum;

public class StakePoolProxyCborConvert<T> : ICborConvertor<StakePoolProxy<T>> where T : IDatum
{
    public StakePoolProxy<T> Read(ref CborReader reader)
    {
        // Assuming there's a starting tag for StakePoolProxy
        var tag = reader.ReadTag();
        if ((int)tag != 121) // Use the appropriate tag for StakePoolProxy
            throw new InvalidOperationException("Unexpected CBOR tag, expected tag for StakePoolProxy.");

        reader.ReadStartArray();

        // Deserialize the Signature
        var signature = new SignatureCborConvert().Read(ref reader);

        // Deserialize the Destination<T>
        var destinationConvertor = (ICborConvertor<Destination<T>>)CborConverter.GetConvertor(typeof(Destination<T>));
        var destination = destinationConvertor.Read(ref reader);

        // Deserialize other fields (LockTime, RewardMultiplier, PolicyId, AssetName, StakeMintingPolicyId)
        var lockTime = reader.ReadUInt64();
        var rewardMultiplier = new RationalCborConvert().Read(ref reader); // Assuming RationalCborConvert exists
        var policyId = reader.ReadByteString();
        var assetName = reader.ReadByteString();
        var assetAmount = reader.ReadUInt64();
        var lovelaceAmount = reader.ReadUInt64();
        var stakeMintingPolicyId = reader.ReadByteString();

        reader.ReadEndArray();

        return new StakePoolProxy<T>(signature, destination, lockTime, rewardMultiplier, policyId, assetName, assetAmount, lovelaceAmount, stakeMintingPolicyId);
    }

    public void Write(ref CborWriter writer, StakePoolProxy<T> value)
    {
        writer.WriteTag((CborTag)121); // Use the appropriate tag for StakePoolProxy
        writer.WriteStartArray(null); // Start of StakePoolProxy array

        // Serialize the Signature
        new SignatureCborConvert().Write(ref writer, value.Owner);

        // Serialize the Destination<T>
        var destinationConvertor = (ICborConvertor<Destination<T>>)CborConverter.GetConvertor(typeof(Destination<T>));
        destinationConvertor.Write(ref writer, value.Destination);

        // Serialize other fields (LockTime, RewardMultiplier, PolicyId, AssetName, StakeMintingPolicyId)
        writer.WriteUInt64(value.LockTime);
        new RationalCborConvert().Write(ref writer, value.RewardMultiplier); // Assuming RationalCborConvert exists
        writer.WriteByteString(value.PolicyId);
        writer.WriteByteString(value.AssetName);
        writer.WriteUInt64(value.AssetAmount);
        writer.WriteUInt64(value.LovelaceAmount);
        writer.WriteByteString(value.StakeMintingPolicyId);

        writer.WriteEndArray(); // End of StakePoolProxy array
    }
}
