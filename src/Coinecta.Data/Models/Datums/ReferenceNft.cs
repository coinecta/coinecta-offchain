using System.Formats.Cbor;
using System.Text;
using Cardano.Sync.Data.Models.Datums;
using CborSerialization;

namespace Coinecta.Data.Models.Datums;

/*
121_0([_
    {
        h'6c6f636b65645f616d6f756e74': h'31303530',
        h'6e616d65': h'5374616b65204e465420314b20434e4354202d20323430333031',
    },
    1,
    121_0([_
        1709296651000_3,
        h'5496b3318f8ca933bbfdf19b8faa7f948d044208e0278d62c24ee73e000de140255321115e9b4a8b78c9d376205aa07b6a4171a5f222ca76cd444239',
    ]),
])
*/
[CborSerialize(typeof(ReferenceNftCborConvert))]
public record ReferenceNft(TimelockMetadata Metadata, ulong Version, Timelock Extra) : IDatum;

public class ReferenceNftCborConvert : ICborConvertor<ReferenceNft>
{
    public ReferenceNft Read(ref CborReader reader)
    {
        var tag = reader.ReadTag();
        if ((int)tag != 121)
        {
            throw new Exception("Invalid tag");
        }

        reader.ReadStartArray();

        // Read the Metadata map
        var metadataMapLength = reader.ReadStartMap();
        ulong lockedAmount = 0;
        var name = string.Empty;
        for (int i = 0; i < metadataMapLength; i++)
        {
            reader.ReadByteString();
            if (i == 0)
            {
                lockedAmount = reader.ReadUInt64();
            }
            else
            {
                name = Convert.ToHexString(reader.ReadByteString());
            }
        }
        var metadata = new TimelockMetadata(lockedAmount, name);
        reader.ReadEndMap();

        // Read the Version
        var version = reader.ReadUInt64();

        // Read the Timelock
        reader.ReadStartArray();
        var lockUntil = reader.ReadUInt64();
        var timeLockKey = reader.ReadByteString();
        var timelock = new Timelock(lockUntil, timeLockKey);
        reader.ReadEndArray();

        reader.ReadEndArray();

        return new ReferenceNft(metadata, version, timelock);
    }

    public void Write(ref CborWriter writer, ReferenceNft value)
    {
        writer.WriteTag((CborTag)121);
        writer.WriteStartArray(3);

        // Write the Metadata
        new TimelockMetadataCborConvert().Write(ref writer, value.Metadata);

        // Write the Version
        writer.WriteUInt64(value.Version);

        // Write the Timelock
        new TimelockCborConvert().Write(ref writer, value.Extra);

        writer.WriteEndArray();
    }
}