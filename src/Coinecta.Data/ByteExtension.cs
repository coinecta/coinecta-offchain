namespace Coinecta.Data;

public static class ByteExtension
{
    public static string ToHex(this byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}