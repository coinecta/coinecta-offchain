using Chrysalis.Cardano.Models.Core.Transaction;
using Coinecta.Data.Models;

namespace Coinecta.Data.Extensions;

public static class ValueExtension
{
    public static MultiAsset ToChrysalisValue(this Value self)
    {
        if (self.MultiAsset is null)
        {
            self.MultiAsset = [];
        }

        if (self.Coin > 0)
        {
            try
            {
                self.MultiAsset.TryAdd(string.Empty, new() { { string.Empty, self.Coin } });
            }
            catch
            {
                self.MultiAsset[string.Empty][string.Empty] = self.Coin;
            }
        }
        else
        {
            try
            {
                self.MultiAsset.Remove(string.Empty);
            }
            catch { }
        }

        return self.MultiAsset.ToChrysalis();
    }
}