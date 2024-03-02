using System.Formats.Cbor;
using System.Text;
using Cardano.Sync.Data.Models.Datums;
using CborSerialization;

namespace Coinecta.Data.Models.Datums;

/*
{
    h'6c6f636b65645f616d6f756e74': h'31303530',
    h'6e616d65': h'5374616b65204e465420314b20434e4354202d20323430333031',
}
*/
[CborSerialize(typeof(TimelockMetadataCborConvert))]
public record TimelockMetadata(ulong LockedAmount, string MetadataName) : IDatum;

public class TimelockMetadataCborConvert : ICborConvertor<TimelockMetadata>
{
    public TimelockMetadata Read(ref CborReader reader)
    {
        var mapLength = reader.ReadStartMap(); // Start reading a map

        ulong lockedAmount = 0L;
        string metadataName = string.Empty;

        for (int i = 0; i < mapLength; i++)
        {
            var keyBytes = reader.ReadByteString();
            var key = BitConverter.ToString(keyBytes).Replace("-", "").ToLowerInvariant(); // Convert bytes to hex string

            if (key == "6c6f636b65645f616d6f756e74")
            {
                lockedAmount = reader.ReadUInt64();

            }
            else if (key == "6e616d65") // "name" in hex
            {
                var valueBytes = reader.ReadByteString();
                metadataName = Encoding.UTF8.GetString(valueBytes);
            }
        }

        reader.ReadEndMap(); // End reading the map

        return new TimelockMetadata(lockedAmount, metadataName);
    }

    public void Write(ref CborWriter writer, TimelockMetadata value)
    {
        writer.WriteStartMap(2);

        // Write the 'locked_amount' key-value pair
        writer.WriteByteString(Convert.FromHexString("6c6f636b65645f616d6f756e74")); // "locked_amount" in hex
        writer.WriteByteString(Encoding.ASCII.GetBytes(value.LockedAmount.ToString()));

        // Write the 'name' key-value pair
        writer.WriteByteString(Convert.FromHexString("6e616d65")); // "name" in hex
        writer.WriteByteString(Convert.FromHexString(value.MetadataName));

        writer.WriteEndMap(); // End the map
    }
}