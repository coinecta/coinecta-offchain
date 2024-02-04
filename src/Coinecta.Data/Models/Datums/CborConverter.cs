

using System.Formats.Cbor;
using CborSerialization;

namespace Coinecta.Data.Models.Datums;
public static class CborConverter
{
    public static byte[] Serialize<T>(T cborObject, CborConformanceMode cborConformanceMode = CborConformanceMode.Lax, bool convertIndefiniteLengthEncodings = false, bool allowMultipleRootLevelValues = false)
    {
        var convertor = GetConvertor(typeof(T));
        CborWriter writer = new(cborConformanceMode, convertIndefiniteLengthEncodings, allowMultipleRootLevelValues);
        ((ICborConvertor<T>)convertor).Write(ref writer, cborObject);
        return writer.Encode();
    }

    public static T Deserialize<T>(byte[] cborData, CborConformanceMode cborConformanceMode = CborConformanceMode.Lax, bool allowMultipleRootLevelValues = false)
    {
        var convertor = GetConvertor(typeof(T));
        CborReader reader = new(cborData, cborConformanceMode, allowMultipleRootLevelValues);
        return ((ICborConvertor<T>)convertor).Read(ref reader);
    }

    public static object GetConvertor(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(InlineDatum<>))
        {
            var innerType = type.GenericTypeArguments[0];
            return Activator.CreateInstance(typeof(InlineDatumCborConvert<>).MakeGenericType(innerType))!;
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Destination<>))
        {
            var innerType = type.GenericTypeArguments[0];
            return Activator.CreateInstance(typeof(DestinationCborConvert<>).MakeGenericType(innerType))!;
        }
        else if (type.GetCustomAttributes(typeof(CborSerialize), inherit: true).Length != 0)
        {
            var attribute = (CborSerialize)type.GetCustomAttributes(typeof(CborSerialize), inherit: true).First();
            return Activator.CreateInstance(attribute.ConvertorType)!;
        }
        else
        {
            throw new CborException($"Type {type.Name} does not have a CborSerialize attribute.");
        }
    }
}
