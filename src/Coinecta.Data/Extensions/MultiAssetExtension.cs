using CardanoSharp.Wallet.Models.Transactions;
using Chrysalis.Cardano.Models.Cbor;
using Chrysalis.Cardano.Models.Core.Transaction;

namespace Coinecta.Data.Extensions;

public static class MultiAssetExtension
{
    public static IEnumerable<(string PolicyId, string AssetName, ulong Quantity)> Flatten(this Dictionary<string, Dictionary<string, ulong>> self) =>
        self.SelectMany(policy =>
            policy.Value.Select(asset => (policy.Key, asset.Key, asset.Value))
        );

    public static Dictionary<byte[], NativeAsset> ToNativeAsset(this Dictionary<string, Dictionary<string, ulong>> self) =>
        self.ToDictionary(
            kvp => Convert.FromHexString(kvp.Key),
            kvp => new NativeAsset
            {
                Token = kvp.Value.ToDictionary(
                    kvp2 => Convert.FromHexString(kvp2.Key),
                    kvp2 => (long)kvp2.Value
                )
            }
        );

    public static Dictionary<string, Dictionary<string, ulong>> Subtract(
        this Dictionary<string, Dictionary<string, ulong>> self,
        Dictionary<string, Dictionary<string, ulong>> other
    )
    {
        foreach (var outerKey in other.Keys)
        {
            if (self.TryGetValue(outerKey, out Dictionary<string, ulong>? value))
            {
                foreach (var innerKey in other[outerKey].Keys)
                {
                    if (value.TryGetValue(innerKey, out ulong innerValue))
                    {
                        // Subtract the value
                        ulong valueToSubtract = other[outerKey][innerKey];
                        if (innerValue >= valueToSubtract)
                        {
                            value[innerKey] -= valueToSubtract;
                        }
                        else
                        {
                            value[innerKey] = 0;
                        }

                        // Remove the inner key if the value is zero
                        if (value[innerKey] == 0)
                        {
                            value.Remove(innerKey);
                        }
                    }
                }

                // Remove the outer key if there are no more inner keys
                if (value.Count == 0)
                {
                    self.Remove(outerKey);
                }
            }
        }

        return self;
    }

    public static MultiAsset ToChrysalis(this Dictionary<string, Dictionary<string, ulong>> self)
    {
        Dictionary<CborBytes, TokenBundle> data = self
                .Where(x => x.Value.Any(y => y.Value != 0))
                .OrderBy(xkvp => xkvp.Key)
                .ToDictionary(
                    x => new CborBytes(Convert.FromHexString(x.Key)),
                    x => new TokenBundle(
                        x.Value
                            .Where(y => y.Value != 0)
                            .OrderBy(ykvp => ykvp.Key)
                            .ToDictionary(
                                y => new CborBytes(Convert.FromHexString(y.Key)),
                                y => new CborUlong(y.Value)
                            )
                    )
                );

        return new(data);
    }
}