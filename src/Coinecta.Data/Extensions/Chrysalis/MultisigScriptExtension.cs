using Chrysalis.Cardano.Models.Sundae;

namespace Coinecta.Data.Extensions.Chrysalis;

public static class MultisigScriptExtension
{
    public static byte[] GetPublicKeyHash(this MultisigScript self) => self switch
    {
        Signature sig => sig.KeyHash.Value,
        _ => throw new Exception("Address type not yet supported")
    };
}