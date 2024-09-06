

using Chrysalis.Cardano.Models.Core;
using Value = Cardano.Sync.Data.Models.Value;

namespace Coinecta.Data.Extensions;

public static class ValueExtension
{
    public static MultiAsset ToChrysalisValue(this Value self)
    {
        self.MultiAsset.Add(string.Empty, new() { { string.Empty, self.Coin } });
        return self.MultiAsset.ToChrysalis();
    }
}