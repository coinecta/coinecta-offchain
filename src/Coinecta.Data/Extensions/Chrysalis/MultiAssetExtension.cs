using Chrysalis.Cardano.Models.Core.Transaction;

namespace Coinecta.Data.Extensions.Chrysalis;

public static class MultiAssetExtension
{
    public static Dictionary<string, Dictionary<string, ulong>> ToDictionary(this MultiAsset multiAsset) => multiAsset.Value.ToDictionary(
        x => Convert.ToHexString(x.Key.Value).ToLowerInvariant(),
        x => x.Value.Value.ToDictionary(
            y => Convert.ToHexString(y.Key.Value).ToLowerInvariant(),
            y => y.Value.Value
        )
    );
}