using CborSerialization;

namespace Coinecta.Data.Models.Datums;

public static class CborConverterResolver
{
    public static ICborConvertor<T> GetConverterFor<T>() where T : IDatum
    {
        var datumType = typeof(T);
        var attribute = datumType
            .GetCustomAttributes(true)
            .Where(a => a is CborSerialize).FirstOrDefault()
                ?? throw new InvalidOperationException($"No CborSerialize attribute found for type {datumType.Name}.");

        var converter = (ICborConvertor<T>)(Activator.CreateInstance(((CborSerialize)attribute).ConvertorType) 
            ?? throw new InvalidOperationException($"Failed to create an instance of {((CborSerialize)attribute).ConvertorType.Name}."));

        return converter;
    }
}
