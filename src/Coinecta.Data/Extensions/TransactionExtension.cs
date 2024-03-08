using System.Formats.Cbor;
using Cardano.Sync.Data.Models.Datums;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using Coinecta.Data.Models.Datums;

public static class TransactionBuilderExtension
{
    public static byte[] Serialize(this Transaction transaction, bool isStandard)
    {
        return isStandard
            ? SerializeStandard(transaction.Serialize())
            : transaction.Serialize();
    }

    private static byte[] SerializeStandard(byte[] txBytes)
    {
        CborReader cborReader = new(txBytes);
        CborWriter cborWriter = new();

        // Start of Tx
        cborReader.ReadStartArray();
        cborWriter.WriteStartArray(4);

        // Start of TxBody
        int? txBodyMapLength = cborReader.ReadStartMap();
        cborWriter.WriteStartMap(txBodyMapLength);
        for (int a = 0; a < txBodyMapLength; a++)
        {
            var txBodyKey = cborReader.ReadUInt64();
            cborWriter.WriteUInt64(txBodyKey);
            switch (txBodyKey)
            {
                case 0: // Inputs
                    var txInputLength = cborReader.ReadStartArray();
                    cborWriter.WriteStartArray(txInputLength);

                    for (int b = 0; b < txInputLength; b++)
                    {
                        cborReader.ReadStartArray();
                        cborWriter.WriteStartArray(2);
                        var txInputHash = cborReader.ReadByteString();
                        cborWriter.WriteByteString(txInputHash);
                        var txInputIndex = cborReader.ReadUInt64();
                        cborWriter.WriteUInt64(txInputIndex);
                        cborReader.ReadEndArray();
                        cborWriter.WriteEndArray();
                    }

                    cborReader.ReadEndArray();
                    cborWriter.WriteEndArray();

                    break;
                case 1: // Outputs
                    var txOutputLength = cborReader.ReadStartArray();
                    cborWriter.WriteStartArray(txOutputLength);
                    for (int b = 0; b < txOutputLength; b++)
                    {
                        var outputTypeState = cborReader.PeekState();
                        if (outputTypeState == CborReaderState.StartMap)
                        {
                            var txOutputMapLength = cborReader.ReadStartMap();
                            cborWriter.WriteStartMap(txOutputMapLength);

                            // Read Address Bytes
                            var addressKey = cborReader.ReadUInt64();
                            cborWriter.WriteUInt64(addressKey);

                            var addressBytes = cborReader.ReadByteString();
                            cborWriter.WriteByteString(addressBytes);

                            // Read Output Value / Amount
                            var valueKey = cborReader.ReadUInt64();
                            cborWriter.WriteUInt64(valueKey);

                            cborReader.ReadStartArray();
                            cborWriter.WriteStartArray(2);

                            var lovelace = cborReader.ReadUInt64();
                            cborWriter.WriteUInt64(lovelace);

                            var multiAssetMapLength = cborReader.ReadStartMap();
                            cborWriter.WriteStartMap(multiAssetMapLength);

                            for (int c = 0; c < multiAssetMapLength; c++)
                            {
                                // Read Policy Id
                                var policyId = cborReader.ReadByteString();
                                cborWriter.WriteByteString(policyId);

                                var assetsMapLength = cborReader.ReadStartMap();
                                cborWriter.WriteStartMap(assetsMapLength);

                                for (int d = 0; d < assetsMapLength; d++)
                                {
                                    var assetName = cborReader.ReadByteString();
                                    cborWriter.WriteByteString(assetName);

                                    var assetValue = cborReader.ReadUInt64();
                                    cborWriter.WriteUInt64(assetValue);
                                }

                                cborReader.ReadEndMap();
                                cborWriter.WriteEndMap();
                            }

                            cborReader.ReadEndMap();
                            cborWriter.WriteEndMap();

                            cborReader.ReadEndArray();
                            cborWriter.WriteEndArray();

                            if (txOutputMapLength == 3)
                            {
                                var datumKey = cborReader.ReadUInt64();
                                cborWriter.WriteUInt64(datumKey);

                                // Read the Datum
                                cborReader.ReadStartArray();
                                cborWriter.WriteStartArray(2);

                                var datumType = cborReader.ReadUInt64();
                                cborWriter.WriteUInt64(datumType);

                                var datumBytesTag = cborReader.ReadTag();
                                cborWriter.WriteTag(datumBytesTag);

                                var datumBytes = cborReader.ReadByteString();
                                if (b == 1)
                                {
                                    var timelockCIP68 = CborConverter.Deserialize<CIP68<Timelock>>(datumBytes);
                                    cborWriter.WriteByteString(CborConverter.Serialize(timelockCIP68));
                                }
                                else
                                {
                                    cborWriter.WriteByteString(datumBytes);
                                }

                                cborReader.ReadEndArray();
                                cborWriter.WriteEndArray();
                            }

                            cborReader.ReadEndMap();
                            cborWriter.WriteEndMap();
                        }
                        else if (outputTypeState == CborReaderState.StartArray)
                        {
                            cborReader.ReadStartArray();
                            cborWriter.WriteStartArray(2);

                            var addressBytes = cborReader.ReadByteString();
                            cborWriter.WriteByteString(addressBytes);

                            var valuePeekState = cborReader.PeekState();

                            if (valuePeekState == CborReaderState.StartArray)
                            {
                                // Read Output Value / Amount
                                cborReader.ReadStartArray();
                                cborWriter.WriteStartArray(2);

                                var lovelace = cborReader.ReadUInt64();
                                cborWriter.WriteUInt64(lovelace);

                                var multiAssetMapLength = cborReader.ReadStartMap();
                                cborWriter.WriteStartMap(multiAssetMapLength);

                                for (int c = 0; c < multiAssetMapLength; c++)
                                {
                                    // Read Policy Id
                                    var policyId = cborReader.ReadByteString();
                                    cborWriter.WriteByteString(policyId);

                                    var assetsMapLength = cborReader.ReadStartMap();
                                    cborWriter.WriteStartMap(assetsMapLength);

                                    for (int d = 0; d < assetsMapLength; d++)
                                    {
                                        var assetName = cborReader.ReadByteString();
                                        cborWriter.WriteByteString(assetName);

                                        var assetValue = cborReader.ReadUInt64();
                                        cborWriter.WriteUInt64(assetValue);
                                    }

                                    cborReader.ReadEndMap();
                                    cborWriter.WriteEndMap();
                                }

                                cborReader.ReadEndMap();
                                cborWriter.WriteEndMap();

                                cborReader.ReadEndArray();
                                cborWriter.WriteEndArray();
                            }
                            else if (valuePeekState == CborReaderState.UnsignedInteger)
                            {
                                var lovelace = cborReader.ReadUInt64();
                                cborWriter.WriteUInt64(lovelace);
                            }

                            cborReader.ReadEndArray();
                            cborWriter.WriteEndArray();
                        }
                    }

                    cborReader.ReadEndArray();
                    cborWriter.WriteEndArray();
                    break;
                case 2: // Fee
                    var feeInLovelace = cborReader.ReadUInt64();
                    cborWriter.WriteUInt64(feeInLovelace);
                    break;
                case 3: // Valid Before TTL
                    var validBefore = cborReader.ReadUInt64();
                    cborWriter.WriteUInt64(validBefore);
                    break;
                case 8: // Valid After
                    var validAfter = cborReader.ReadUInt64();
                    cborWriter.WriteUInt64(validAfter);
                    break;
                case 9: // Mint (Map for Multi Assets to be minted)
                    var mintMapLength = cborReader.ReadStartMap();
                    cborWriter.WriteStartMap(mintMapLength);
                    for (int b = 0; b < mintMapLength; b++)
                    {
                        var policyId = cborReader.ReadByteString();
                        cborWriter.WriteByteString(policyId);

                        var assetsMapLength = cborReader.ReadStartMap();
                        cborWriter.WriteStartMap(assetsMapLength);

                        for (int c = 0; c < assetsMapLength; c++)
                        {
                            var assetName = cborReader.ReadByteString();
                            cborWriter.WriteByteString(assetName);

                            var assetValue = cborReader.ReadUInt64();
                            cborWriter.WriteUInt64(assetValue);
                        }

                        cborReader.ReadEndMap();
                        cborWriter.WriteEndMap();
                    }
                    cborReader.ReadEndMap();
                    cborWriter.WriteEndMap();
                    break;
                case 11: // Script Data Hash
                    var scriptDataHash = cborReader.ReadByteString();
                    cborWriter.WriteByteString(scriptDataHash);
                    break;
                case 13: // Collateral Inputs
                    var collateralInputsLength = cborReader.ReadStartArray();
                    cborWriter.WriteStartArray(collateralInputsLength);
                    for (int b = 0; b < collateralInputsLength; b++)
                    {
                        cborReader.ReadStartArray();
                        cborWriter.WriteStartArray(2);
                        var txInputHash = cborReader.ReadByteString();
                        cborWriter.WriteByteString(txInputHash);
                        var txInputIndex = cborReader.ReadUInt64();
                        cborWriter.WriteUInt64(txInputIndex);
                        cborReader.ReadEndArray();
                        cborWriter.WriteEndArray();
                    }
                    cborReader.ReadEndArray();
                    cborWriter.WriteEndArray();
                    break;
                case 18: // Reference Inputs
                    var referenceInputsLength = cborReader.ReadStartArray();
                    cborWriter.WriteStartArray(referenceInputsLength);
                    for (int b = 0; b < referenceInputsLength; b++)
                    {
                        cborReader.ReadStartArray();
                        cborWriter.WriteStartArray(2);
                        var txInputHash = cborReader.ReadByteString();
                        cborWriter.WriteByteString(txInputHash);
                        var txInputIndex = cborReader.ReadUInt64();
                        cborWriter.WriteUInt64(txInputIndex);
                        cborReader.ReadEndArray();
                        cborWriter.WriteEndArray();
                    }
                    cborReader.ReadEndArray();
                    cborWriter.WriteEndArray();
                    break;
            }
        }

        // End of Tx Body
        cborReader.ReadEndMap();
        cborWriter.WriteEndMap();

        // Witness Sets
        var witnessMapLength = cborReader.ReadStartMap();
        cborWriter.WriteStartMap(witnessMapLength);

        for (int a = 0; a < witnessMapLength; a++)
        {
            var witnessKey = cborReader.ReadUInt64();
            cborWriter.WriteUInt64(witnessKey);

            switch (witnessKey)
            {
                case 0:
                    var vkeyWitnessLength = cborReader.ReadStartArray();
                    cborWriter.WriteStartArray(vkeyWitnessLength);

                    for (int b = 0; b < vkeyWitnessLength; b++)
                    {
                        cborReader.ReadStartArray();
                        cborWriter.WriteStartArray(2);
                        var vkey = cborReader.ReadByteString();
                        cborWriter.WriteByteString(vkey);
                        var signature = cborReader.ReadByteString();
                        cborWriter.WriteByteString(signature);
                        cborReader.ReadEndArray();
                        cborWriter.WriteEndArray();
                    }

                    cborReader.ReadEndArray();
                    cborWriter.WriteEndArray();
                    break;
                case 5:
                    var redeemerWitnessLength = cborReader.ReadStartArray();
                    cborWriter.WriteStartArray(redeemerWitnessLength);

                    for (int b = 0; b < redeemerWitnessLength; b++)
                    {
                        // Start of Redeemer Witness
                        cborReader.ReadStartArray();
                        cborWriter.WriteStartArray(4);

                        var redeemerTag = cborReader.ReadUInt64();
                        cborWriter.WriteUInt64(redeemerTag);

                        var redeemerIndex = cborReader.ReadUInt64();
                        cborWriter.WriteUInt64(redeemerIndex);

                        // Write Redeemer Data
                        CopyNextElement(cborReader, cborWriter);

                        // ExUnits
                        cborReader.ReadStartArray();
                        cborWriter.WriteStartArray(2);

                        var mem = cborReader.ReadUInt64();
                        cborWriter.WriteUInt64(mem);

                        var steps = cborReader.ReadUInt64();
                        cborWriter.WriteUInt64(steps);

                        cborReader.ReadEndArray();
                        cborWriter.WriteEndArray();

                        // End of Redeemer Witness
                        cborReader.ReadEndArray();
                        cborWriter.WriteEndArray();
                    }

                    cborReader.ReadEndArray();
                    cborWriter.WriteEndArray();
                    break;
            }
        }

        cborReader.ReadEndMap();
        cborWriter.WriteEndMap();

        // isValid
        cborWriter.WriteBoolean(true);

        // Auxiliary Data
        cborWriter.WriteNull();

        // End of Tx
        cborWriter.WriteEndArray();

        return cborWriter.Encode();
    }

    private static void CopyNextElement(CborReader reader, CborWriter writer)
    {
        switch (reader.PeekState())
        {
            case CborReaderState.StartArray:
                int? length = reader.ReadStartArray();
                writer.WriteStartArray(length);
                while (reader.PeekState() != CborReaderState.EndArray)
                {
                    CopyNextElement(reader, writer); // Recursively copy elements within the array
                }
                reader.ReadEndArray();
                writer.WriteEndArray();
                break;

            case CborReaderState.StartMap:
                reader.ReadStartMap();
                writer.WriteStartMap(reader.CurrentDepth);
                while (reader.PeekState() != CborReaderState.EndMap)
                {
                    CopyNextElement(reader, writer); // Recursively copy key-value pairs within the map
                    CopyNextElement(reader, writer);
                }
                reader.ReadEndMap();
                writer.WriteEndMap();
                break;

            case CborReaderState.Tag:
                var tag = reader.ReadTag();
                writer.WriteTag(tag);
                CopyNextElement(reader, writer); // Copy the data associated with the tag
                break;

            // Handle simple types (UnsignedInteger, NegativeInteger, ByteString, TextString, etc.)
            case CborReaderState.UnsignedInteger:
                writer.WriteUInt64(reader.ReadUInt64());
                break;

            case CborReaderState.ByteString:
                writer.WriteByteString(reader.ReadByteString());
                break;

            case CborReaderState.TextString:
                writer.WriteTextString(reader.ReadTextString());
                break;

            // Add cases for other simple types as needed (NegativeInteger, SimpleValue, etc.)

            default:
                throw new InvalidOperationException($"Unsupported or unexpected CBOR data type: {reader.PeekState()}");
        }
    }
}