using System.Formats.Cbor;
using Cardano.Sync.Data.Models.Datums;
using CborSerialization;

namespace Coinecta.Data.Models.Datums;


[CborSerialize(typeof(OutputReferenceCborConvert))]
public record OutputReference(byte[] TransactionId, ulong OutputIndex) : IDatum;
public class OutputReferenceCborConvert : ICborConvertor<OutputReference>
{
    public OutputReference Read(ref CborReader reader)
    {
        var tag = reader.ReadTag();
        if ((int)tag != 121) // Adjust the tag number as necessary
        {
            throw new Exception("Invalid tag");
        }

        reader.ReadStartArray(); // Start the outer array

        var transactionId = reader.ReadByteString();
        var outputIndex = reader.ReadUInt64();

        reader.ReadEndArray(); // End the outer array

        return new OutputReference(transactionId, outputIndex);
    }

    public void Write(ref CborWriter writer, OutputReference value)
    {
        writer.WriteTag((CborTag)121); // Adjust the tag number as necessary

        writer.WriteStartArray(2); // Start the outer array
        writer.WriteByteString(value.TransactionId);
        writer.WriteUInt64(value.OutputIndex);
        writer.WriteEndArray(); // End the outer array
    }
}