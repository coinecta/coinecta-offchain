using CardanoSharp.Wallet.Models.Transactions;

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
}