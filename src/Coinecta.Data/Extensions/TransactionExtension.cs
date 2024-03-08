using System.Formats.Cbor;
using Cardano.Sync.Data.Models.Datums;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Utilities;
using Coinecta.Data.Models.Datums;

namespace Coinecta.Data.Extensions;

public static class TransactionExtension
{
    public static byte[] SignAndSerializeStakeExecuteTx(this Transaction transaction, VKeyWitness vkeyWitness)
    {
        CborWriter txCborWriter = new();
        var (txBodyBytes, txWitnessBytes) = SerializeStandard(transaction.Serialize());
        var txHash = HashUtility.Blake2b256(txBodyBytes);
        var pubKey = vkeyWitness.VKey.Key;
        var signature = vkeyWitness.SKey.Sign(txHash);
        txCborWriter.WriteStartArray(4);

        // Write Transaction Body
        txCborWriter.WriteEncodedValue(txBodyBytes);
        txCborWriter.WriteEncodedValue(InsertVkeyToWitness(txWitnessBytes, pubKey, signature));

        txCborWriter.WriteBoolean(true); // Metadata
        txCborWriter.WriteNull(); // Auxiliary Data

        txCborWriter.WriteEndArray();

        return txCborWriter.Encode();
    }

    public static byte[] InsertVkeyToWitness(byte[] txWitnessBytes, byte[] newVkey, byte[] newSignature)
    {
        CborReader cborReader = new(txWitnessBytes);
        CborWriter txWitnessCborWriter = new();
        List<(byte[], byte[])> vkeyWitnesses = [];
        List<byte[]> redeemerWitnesses = [];

        // Insert Vkey Witness
        vkeyWitnesses.Add((newVkey, newSignature));

        var witnessMapLength = cborReader.ReadStartMap();

        for (int a = 0; a < witnessMapLength; a++)
        {
            var witnessKey = cborReader.ReadUInt64();

            switch (witnessKey)
            {
                case 0:
                    var vkeyWitnessLength = cborReader.ReadStartArray();

                    for (int b = 0; b < vkeyWitnessLength; b++)
                    {
                        cborReader.ReadStartArray();
                        var vkey = cborReader.ReadByteString();
                        var signature = cborReader.ReadByteString();
                        vkeyWitnesses.Add((vkey, signature));
                        cborReader.ReadEndArray();
                    }

                    cborReader.ReadEndArray();
                    break;
                case 5:
                    var redeemerWitnessLength = cborReader.ReadStartArray();
                    for (int b = 0; b < redeemerWitnessLength; b++)
                    {
                        var redeemerEncodedValue = cborReader.ReadEncodedValue().Span;
                        redeemerWitnesses.Add(redeemerEncodedValue.ToArray());
                    }
                    break;
            }
        }

        txWitnessCborWriter.WriteStartMap(2);
        txWitnessCborWriter.WriteUInt64(0);
        txWitnessCborWriter.WriteStartArray(vkeyWitnesses.Count);

        foreach (var (vkey, signature) in vkeyWitnesses)
        {
            txWitnessCborWriter.WriteStartArray(2);
            txWitnessCborWriter.WriteByteString(vkey);
            txWitnessCborWriter.WriteByteString(signature);
            txWitnessCborWriter.WriteEndArray();
        }

        txWitnessCborWriter.WriteEndArray();

        txWitnessCborWriter.WriteUInt64(5);
        txWitnessCborWriter.WriteStartArray(redeemerWitnesses.Count);

        foreach (var redeemer in redeemerWitnesses)
        {
            txWitnessCborWriter.WriteEncodedValue(redeemer);
        }

        txWitnessCborWriter.WriteEndArray();

        return txWitnessBytes;
    }

    private static (byte[], byte[]) SerializeStandard(byte[] txBytes)
    {
        CborReader cborReader = new(txBytes);
        CborWriter txBodyCborWriter = new();
        CborWriter txWitnessCborWriter = new();

        // Start of Tx
        cborReader.ReadStartArray();

        // Start of TxBody
        int? txBodyMapLength = cborReader.ReadStartMap();
        txBodyCborWriter.WriteStartMap(txBodyMapLength);
        for (int a = 0; a < txBodyMapLength; a++)
        {
            var txBodyKey = cborReader.ReadUInt64();
            txBodyCborWriter.WriteUInt64(txBodyKey);
            switch (txBodyKey)
            {
                case 0: // Inputs
                    var txInputLength = cborReader.ReadStartArray();
                    txBodyCborWriter.WriteStartArray(txInputLength);

                    for (int b = 0; b < txInputLength; b++)
                    {
                        cborReader.ReadStartArray();
                        txBodyCborWriter.WriteStartArray(2);
                        var txInputHash = cborReader.ReadByteString();
                        txBodyCborWriter.WriteByteString(txInputHash);
                        var txInputIndex = cborReader.ReadUInt64();
                        txBodyCborWriter.WriteUInt64(txInputIndex);
                        cborReader.ReadEndArray();
                        txBodyCborWriter.WriteEndArray();
                    }

                    cborReader.ReadEndArray();
                    txBodyCborWriter.WriteEndArray();

                    break;
                case 1: // Outputs
                    var txOutputLength = cborReader.ReadStartArray();
                    txBodyCborWriter.WriteStartArray(txOutputLength);
                    for (int b = 0; b < txOutputLength; b++)
                    {
                        var outputTypeState = cborReader.PeekState();
                        if (outputTypeState == CborReaderState.StartMap)
                        {
                            var txOutputMapLength = cborReader.ReadStartMap();
                            txBodyCborWriter.WriteStartMap(txOutputMapLength);

                            // Read Address Bytes
                            var addressKey = cborReader.ReadUInt64();
                            txBodyCborWriter.WriteUInt64(addressKey);

                            var addressBytes = cborReader.ReadByteString();
                            txBodyCborWriter.WriteByteString(addressBytes);

                            // Read Output Value / Amount
                            var valueKey = cborReader.ReadUInt64();
                            txBodyCborWriter.WriteUInt64(valueKey);

                            cborReader.ReadStartArray();
                            txBodyCborWriter.WriteStartArray(2);

                            var lovelace = cborReader.ReadUInt64();
                            txBodyCborWriter.WriteUInt64(lovelace);

                            var multiAssetMapLength = cborReader.ReadStartMap();
                            txBodyCborWriter.WriteStartMap(multiAssetMapLength);

                            for (int c = 0; c < multiAssetMapLength; c++)
                            {
                                // Read Policy Id
                                var policyId = cborReader.ReadByteString();
                                txBodyCborWriter.WriteByteString(policyId);

                                var assetsMapLength = cborReader.ReadStartMap();
                                txBodyCborWriter.WriteStartMap(assetsMapLength);

                                for (int d = 0; d < assetsMapLength; d++)
                                {
                                    var assetName = cborReader.ReadByteString();
                                    txBodyCborWriter.WriteByteString(assetName);

                                    var assetValue = cborReader.ReadUInt64();
                                    txBodyCborWriter.WriteUInt64(assetValue);
                                }

                                cborReader.ReadEndMap();
                                txBodyCborWriter.WriteEndMap();
                            }

                            cborReader.ReadEndMap();
                            txBodyCborWriter.WriteEndMap();

                            cborReader.ReadEndArray();
                            txBodyCborWriter.WriteEndArray();

                            if (txOutputMapLength == 3)
                            {
                                var datumKey = cborReader.ReadUInt64();
                                txBodyCborWriter.WriteUInt64(datumKey);

                                // Read the Datum
                                cborReader.ReadStartArray();
                                txBodyCborWriter.WriteStartArray(2);

                                var datumType = cborReader.ReadUInt64();
                                txBodyCborWriter.WriteUInt64(datumType);

                                var datumBytesTag = cborReader.ReadTag();
                                txBodyCborWriter.WriteTag(datumBytesTag);

                                var datumBytes = cborReader.ReadByteString();
                                if (b == 1)
                                {
                                    var timelockCIP68 = CborConverter.Deserialize<CIP68<Timelock>>(datumBytes);
                                    txBodyCborWriter.WriteByteString(CborConverter.Serialize(timelockCIP68));
                                }
                                else
                                {
                                    txBodyCborWriter.WriteByteString(datumBytes);
                                }

                                cborReader.ReadEndArray();
                                txBodyCborWriter.WriteEndArray();
                            }

                            cborReader.ReadEndMap();
                            txBodyCborWriter.WriteEndMap();
                        }
                        else if (outputTypeState == CborReaderState.StartArray)
                        {
                            cborReader.ReadStartArray();
                            txBodyCborWriter.WriteStartArray(2);

                            var addressBytes = cborReader.ReadByteString();
                            txBodyCborWriter.WriteByteString(addressBytes);

                            var valuePeekState = cborReader.PeekState();

                            if (valuePeekState == CborReaderState.StartArray)
                            {
                                // Read Output Value / Amount
                                cborReader.ReadStartArray();
                                txBodyCborWriter.WriteStartArray(2);

                                var lovelace = cborReader.ReadUInt64();
                                txBodyCborWriter.WriteUInt64(lovelace);

                                var multiAssetMapLength = cborReader.ReadStartMap();
                                txBodyCborWriter.WriteStartMap(multiAssetMapLength);

                                for (int c = 0; c < multiAssetMapLength; c++)
                                {
                                    // Read Policy Id
                                    var policyId = cborReader.ReadByteString();
                                    txBodyCborWriter.WriteByteString(policyId);

                                    var assetsMapLength = cborReader.ReadStartMap();
                                    txBodyCborWriter.WriteStartMap(assetsMapLength);

                                    for (int d = 0; d < assetsMapLength; d++)
                                    {
                                        var assetName = cborReader.ReadByteString();
                                        txBodyCborWriter.WriteByteString(assetName);

                                        var assetValue = cborReader.ReadUInt64();
                                        txBodyCborWriter.WriteUInt64(assetValue);
                                    }

                                    cborReader.ReadEndMap();
                                    txBodyCborWriter.WriteEndMap();
                                }

                                cborReader.ReadEndMap();
                                txBodyCborWriter.WriteEndMap();

                                cborReader.ReadEndArray();
                                txBodyCborWriter.WriteEndArray();
                            }
                            else if (valuePeekState == CborReaderState.UnsignedInteger)
                            {
                                var lovelace = cborReader.ReadUInt64();
                                txBodyCborWriter.WriteUInt64(lovelace);
                            }

                            cborReader.ReadEndArray();
                            txBodyCborWriter.WriteEndArray();
                        }
                    }

                    cborReader.ReadEndArray();
                    txBodyCborWriter.WriteEndArray();
                    break;
                case 2: // Fee
                    var feeInLovelace = cborReader.ReadUInt64();
                    txBodyCborWriter.WriteUInt64(feeInLovelace);
                    break;
                case 3: // Valid Before TTL
                    var validBefore = cborReader.ReadUInt64();
                    txBodyCborWriter.WriteUInt64(validBefore);
                    break;
                case 8: // Valid After
                    var validAfter = cborReader.ReadUInt64();
                    txBodyCborWriter.WriteUInt64(validAfter);
                    break;
                case 9: // Mint (Map for Multi Assets to be minted)
                    var mintMapLength = cborReader.ReadStartMap();
                    txBodyCborWriter.WriteStartMap(mintMapLength);
                    for (int b = 0; b < mintMapLength; b++)
                    {
                        var policyId = cborReader.ReadByteString();
                        txBodyCborWriter.WriteByteString(policyId);

                        var assetsMapLength = cborReader.ReadStartMap();
                        txBodyCborWriter.WriteStartMap(assetsMapLength);

                        for (int c = 0; c < assetsMapLength; c++)
                        {
                            var assetName = cborReader.ReadByteString();
                            txBodyCborWriter.WriteByteString(assetName);

                            var assetValue = cborReader.ReadUInt64();
                            txBodyCborWriter.WriteUInt64(assetValue);
                        }

                        cborReader.ReadEndMap();
                        txBodyCborWriter.WriteEndMap();
                    }
                    cborReader.ReadEndMap();
                    txBodyCborWriter.WriteEndMap();
                    break;
                case 11: // Script Data Hash
                    var scriptDataHash = cborReader.ReadByteString();
                    txBodyCborWriter.WriteByteString(scriptDataHash);
                    break;
                case 13: // Collateral Inputs
                    var collateralInputsLength = cborReader.ReadStartArray();
                    txBodyCborWriter.WriteStartArray(collateralInputsLength);
                    for (int b = 0; b < collateralInputsLength; b++)
                    {
                        cborReader.ReadStartArray();
                        txBodyCborWriter.WriteStartArray(2);
                        var txInputHash = cborReader.ReadByteString();
                        txBodyCborWriter.WriteByteString(txInputHash);
                        var txInputIndex = cborReader.ReadUInt64();
                        txBodyCborWriter.WriteUInt64(txInputIndex);
                        cborReader.ReadEndArray();
                        txBodyCborWriter.WriteEndArray();
                    }
                    cborReader.ReadEndArray();
                    txBodyCborWriter.WriteEndArray();
                    break;
                case 18: // Reference Inputs
                    var referenceInputsLength = cborReader.ReadStartArray();
                    txBodyCborWriter.WriteStartArray(referenceInputsLength);
                    for (int b = 0; b < referenceInputsLength; b++)
                    {
                        cborReader.ReadStartArray();
                        txBodyCborWriter.WriteStartArray(2);
                        var txInputHash = cborReader.ReadByteString();
                        txBodyCborWriter.WriteByteString(txInputHash);
                        var txInputIndex = cborReader.ReadUInt64();
                        txBodyCborWriter.WriteUInt64(txInputIndex);
                        cborReader.ReadEndArray();
                        txBodyCborWriter.WriteEndArray();
                    }
                    cborReader.ReadEndArray();
                    txBodyCborWriter.WriteEndArray();
                    break;
            }
        }

        // End of Tx Body
        cborReader.ReadEndMap();
        txBodyCborWriter.WriteEndMap();

        // Witness Sets
        var witnessMapLength = cborReader.ReadStartMap();
        txWitnessCborWriter.WriteStartMap(witnessMapLength);

        for (int a = 0; a < witnessMapLength; a++)
        {
            var witnessKey = cborReader.ReadUInt64();
            txWitnessCborWriter.WriteUInt64(witnessKey);

            switch (witnessKey)
            {
                case 0:
                    var vkeyWitnessLength = cborReader.ReadStartArray();
                    txWitnessCborWriter.WriteStartArray(vkeyWitnessLength);

                    for (int b = 0; b < vkeyWitnessLength; b++)
                    {
                        cborReader.ReadStartArray();
                        txWitnessCborWriter.WriteStartArray(2);
                        var vkey = cborReader.ReadByteString();
                        txWitnessCborWriter.WriteByteString(vkey);
                        var signature = cborReader.ReadByteString();
                        txWitnessCborWriter.WriteByteString(signature);
                        cborReader.ReadEndArray();
                        txWitnessCborWriter.WriteEndArray();
                    }

                    cborReader.ReadEndArray();
                    txWitnessCborWriter.WriteEndArray();
                    break;
                case 5:
                    var redeemerWitnessLength = cborReader.ReadStartArray();
                    txWitnessCborWriter.WriteStartArray(redeemerWitnessLength);

                    for (int b = 0; b < redeemerWitnessLength; b++)
                    {
                        // Start of Redeemer Witness
                        cborReader.ReadStartArray();
                        txWitnessCborWriter.WriteStartArray(4);

                        var redeemerTag = cborReader.ReadUInt64();
                        txWitnessCborWriter.WriteUInt64(redeemerTag);

                        var redeemerIndex = cborReader.ReadUInt64();
                        txWitnessCborWriter.WriteUInt64(redeemerIndex);

                        // Write Redeemer Data
                        txWitnessCborWriter.WriteEncodedValue(cborReader.ReadEncodedValue().Span);

                        // ExUnits
                        cborReader.ReadStartArray();
                        txWitnessCborWriter.WriteStartArray(2);

                        var mem = cborReader.ReadUInt64();
                        txWitnessCborWriter.WriteUInt64(mem);

                        var steps = cborReader.ReadUInt64();
                        txWitnessCborWriter.WriteUInt64(steps);

                        cborReader.ReadEndArray();
                        txWitnessCborWriter.WriteEndArray();

                        // End of Redeemer Witness
                        cborReader.ReadEndArray();
                        txWitnessCborWriter.WriteEndArray();
                    }

                    cborReader.ReadEndArray();
                    txWitnessCborWriter.WriteEndArray();
                    break;
            }
        }

        // End of Witness Sets
        txWitnessCborWriter.WriteEndMap();

        var txBodyCborBytes = txBodyCborWriter.Encode();
        var txWitnessCborBytes = txWitnessCborWriter.Encode();
        return (txBodyCborBytes, txWitnessCborBytes);
    }
}